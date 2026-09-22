using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Serilog;

namespace SoundBox.Audio;

public sealed class AudioManager : IDisposable
{
    private readonly object _sync = new();
    private readonly ILogger _logger;
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private PlaybackSession? _current;

    public AudioManager(ILogger logger)
    {
        _logger = logger.ForContext<AudioManager>();
    }

    public IReadOnlyList<AudioDevice> GetOutputDevices()
    {
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
            _current = null;
        }

        session?.Dispose();
    }

    private void OnSessionFinished(PlaybackSession session)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_current, session))
            {
                _current = null;
            }
        }
    }

    private MMDevice? FindDevice(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.Equals(deviceId, "default", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                _logger.Warning(ex, "Default audio endpoint could not be retrieved.");
                return null;
            }
        }

        try
        {
            return _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
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
        _deviceEnumerator.Dispose();
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
                    _players.Add((reader, output));
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

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopping, 1) != 0)
            {
                return;
            }

            _onFinished?.Invoke(this);

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
