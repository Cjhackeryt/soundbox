using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;

namespace SoundBox.Audio;

public sealed class AudioManager : IDisposable
{
    private readonly object _sync = new();
    private readonly ILogger _logger;
    private readonly MMDeviceEnumerator? _deviceEnumerator;
    private PlaybackSession? _current;
    private long _playbackVersion;

    public AudioManager(ILogger logger)
    {
        _logger = logger.ForContext<AudioManager>();
        if (OperatingSystem.IsWindows())
        {
            _deviceEnumerator = new MMDeviceEnumerator();
        }
    }

    public IReadOnlyList<AudioDevice> GetOutputDevices()
    {
        if (_deviceEnumerator is null)
        {
            return [];
        }

        return _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(device => new AudioDevice(device.ID, device.FriendlyName))
            .ToArray();
    }

    public bool Play(string filePath, string? outputDeviceId, bool monitor, int volumePercent, bool loop)
    {
        if (!File.Exists(filePath))
        {
            _logger.Warning("Sound file does not exist: {FilePath}", filePath);
            return false;
        }

        if (_deviceEnumerator is null)
        {
            _logger.Warning("Audio playback is only supported on Windows.");
            return false;
        }

        Stop();

        try
        {
            var outputs = new List<MMDevice>();
            var selectedOutput = FindDevice(outputDeviceId);
            if (selectedOutput is not null)
            {
                outputs.Add(selectedOutput);
            }

            if (monitor)
            {
                try
                {
                    var monitorOutput = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    if (outputs.All(device => !string.Equals(device.ID, monitorOutput.ID, StringComparison.OrdinalIgnoreCase)))
                    {
                        outputs.Add(monitorOutput);
                    }
                }
                catch (Exception ex) when (ex is COMException or InvalidOperationException)
                {
                    _logger.Warning(ex, "Unable to acquire default audio endpoint for monitoring.");
                }
            }

            if (outputs.Count == 0)
            {
                try
                {
                    var fallback = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                    outputs.Add(fallback);
                }
                catch (Exception ex) when (ex is COMException or InvalidOperationException)
                {
                    _logger.Warning(ex, "Unable to acquire fallback audio endpoint.");
                }
            }

            if (outputs.Count == 0)
            {
                _logger.Warning("No active audio output device is available.");
                return false;
            }

            var session = new PlaybackSession(filePath, outputs, Math.Clamp(volumePercent, 0, 100) / 100f, loop, _logger, OnSessionFinished);
            lock (_sync)
            {
                _current = session;
                _playbackVersion++;
            }

            session.Start();
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException or COMException)
        {
            _logger.Error(exception, "Unable to start sound playback for {FilePath}", filePath);
            return false;
        }
    }

    public void Stop()
    {
        PlaybackSession? session;
        lock (_sync)
        {
            session = _current;
            if (session is not null)
            {
                _current = null;
                _playbackVersion++;
            }
        }

        session?.Dispose();
    }

    /// <summary>
    /// Monotonic id of the current playback. It advances whenever the active playback is
    /// replaced, stopped or finishes, so a caller can tell that the playback it was
    /// observing is no longer the one running.
    /// </summary>
    public long PlaybackVersion
    {
        get
        {
            lock (_sync)
            {
                return _playbackVersion;
            }
        }
    }

    /// <summary>
    /// Reports the time left in the active playback, derived from the reader's own position
    /// rather than a separate clock, so the countdown tracks the audio the device is consuming.
    /// </summary>
    public bool TryGetRemainingPlaybackTime(out TimeSpan remaining) =>
        TryGetRemainingPlaybackTime(out remaining, out _);

    /// <summary>
    /// Reports the time left in the active playback together with the id of that playback.
    /// Returns false when nothing is playing.
    /// </summary>
    public bool TryGetRemainingPlaybackTime(out TimeSpan remaining, out long playbackVersion)
    {
        remaining = TimeSpan.Zero;

        PlaybackSession? session;
        lock (_sync)
        {
            session = _current;
            playbackVersion = _playbackVersion;
        }

        return session is not null && session.TryGetRemaining(out remaining);
    }

    private void OnSessionFinished(PlaybackSession session)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_current, session))
            {
                _current = null;
                _playbackVersion++;
            }
        }
    }

    private MMDevice? FindDevice(string? deviceId)
    {
        var deviceEnumerator = _deviceEnumerator;
        if (deviceEnumerator is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(deviceId) || string.Equals(deviceId, "default", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                _logger.Warning(ex, "Default audio endpoint could not be retrieved.");
                return null;
            }
        }

        try
        {
            return deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .FirstOrDefault(device => string.Equals(device.ID, deviceId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            _logger.Warning(ex, "Error searching for audio endpoint {DeviceId}.", deviceId);
            return null;
        }
    }

    public void Dispose()
    {
        Stop();
        _deviceEnumerator?.Dispose();
    }

    public sealed record AudioDevice(string Id, string Name);

    private sealed class PlaybackSession : IDisposable
    {
        private readonly string _filePath;
        private readonly IReadOnlyList<MMDevice> _outputs;
        private readonly float _volume;
        private readonly bool _loop;
        private readonly ILogger _logger;
        private readonly Action<PlaybackSession>? _onFinished;
        private readonly List<(AudioFileReader Reader, WasapiOut Output)> _players = [];
        // Guards _players so the countdown read and reader disposal cannot observe each other mid-flight.
        private readonly object _positionSync = new();
        private int _stopping;

        public PlaybackSession(string filePath, IReadOnlyList<MMDevice> outputs, float volume, bool loop, ILogger logger, Action<PlaybackSession>? onFinished = null)
        {
            _filePath = filePath;
            _outputs = outputs;
            _volume = volume;
            _loop = loop;
            _logger = logger;
            _onFinished = onFinished;
        }

        public void Start()
        {
            try
            {
                foreach (var device in _outputs)
                {
                    var reader = new AudioFileReader(_filePath) { Volume = _volume };
                    var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
                    output.PlaybackStopped += (_, _) => HandlePlaybackStopped(reader, output);
                    lock (_positionSync)
                    {
                        _players.Add((reader, output));
                    }
                    output.Init(reader);
                    output.Play();
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void HandlePlaybackStopped(AudioFileReader reader, WasapiOut output)
        {
            if (_loop && Volatile.Read(ref _stopping) == 0)
            {
                try
                {
                    reader.Position = 0;
                    output.Play();
                    return;
                }
                catch (Exception exception) when (exception is IOException or InvalidOperationException)
                {
                    _logger.Warning(exception, "Unable to loop sound playback.");
                }
            }

            if (_players.All(player => player.Output.PlaybackState == PlaybackState.Stopped))
            {
                // Must be disposed asynchronously to avoid deadlock with WasapiOut's audio playback thread
                Task.Run(() => Dispose());
            }
        }

        public bool TryGetRemaining(out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;

            lock (_positionSync)
            {
                // Disposal nulls the reader's inner stream, so a stopped session must never be sampled.
                if (Volatile.Read(ref _stopping) != 0 || _players.Count == 0)
                {
                    return false;
                }

                try
                {
                    var reader = _players[0].Reader;
                    remaining = reader.TotalTime - reader.CurrentTime;
                    if (remaining < TimeSpan.Zero)
                    {
                        remaining = TimeSpan.Zero;
                    }

                    return true;
                }
                catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException)
                {
                    _logger.Debug(exception, "Unable to sample the SoundBox playback position.");
                    return false;
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopping, 1) != 0)
            {
                return;
            }

            _onFinished?.Invoke(this);

            lock (_positionSync)
            {
                foreach (var (reader, output) in _players)
                {
                    try
                    {
                        output.Stop();
                        output.Dispose();
                        reader.Dispose();
                    }
                    catch (Exception exception)
                    {
                        _logger.Warning(exception, "Error disposing audio player resources.");
                    }
                }

                _players.Clear();
            }

            foreach (var device in _outputs)
            {
                try
                {
                    device.Dispose();
                }
                catch (Exception exception)
                {
                    _logger.Warning(exception, "Error disposing audio device endpoint.");
                }
            }
        }
    }
}
