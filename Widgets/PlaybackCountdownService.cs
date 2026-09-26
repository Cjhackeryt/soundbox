using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Sdk.Widgets;
using Serilog;
using SoundBox.Audio;
using SoundBox.Playback;

namespace SoundBox.Widgets;

/// <summary>
/// Drives a live countdown on the Macro Deck widget whose Play Sound action was pressed.
/// Every widget gets its own independent session with its own update loop, so one widget's
/// countdown never drives another's appearance. A session ends when the playback it was bound
/// to is replaced, stopped or finishes, at which point the widget's configured appearance is
/// restored.
/// </summary>
public sealed class PlaybackCountdownService : IDisposable
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(300);

    private readonly AudioManager _audioManager;
    private readonly ILogger _logger;
    private readonly object _sync = new();
    private readonly Dictionary<string, CountdownSession> _sessions = new(StringComparer.Ordinal);
    private IWidgetApi? _widgets;
    private int _disposed;

    public PlaybackCountdownService(AudioManager audioManager, ILogger logger)
    {
        _audioManager = audioManager;
        _logger = logger.ForContext<PlaybackCountdownService>();
    }

    /// <summary>
    /// Binds the host widget API. InitializeAsync can run again after a reconnect, so this
    /// replaces whatever was bound before.
    /// </summary>
    public void Attach(IWidgetApi widgets)
    {
        lock (_sync)
        {
            _widgets = widgets;
        }
    }

    /// <summary>
    /// Starts counting down on <paramref name="widgetId"/> for the playback running now,
    /// replacing any countdown that widget already had. No-op when the host is unavailable.
    /// </summary>
    public async Task StartAsync(string widgetId, CancellationToken cancellationToken)
    {
        CountdownSession? superseded;
        CountdownSession session;

        lock (_sync)
        {
            if (Volatile.Read(ref _disposed) != 0 || _widgets is null)
            {
                return;
            }

            _sessions.Remove(widgetId, out superseded);

            session = new CountdownSession(widgetId, _audioManager.PlaybackVersion, _audioManager, _widgets, _logger, OnSessionFinished);
            _sessions[widgetId] = session;
        }

        superseded?.Stop();

        // Show the full duration straight away instead of waiting for the first tick.
        if (_audioManager.TryGetRemainingPlaybackTime(out var remaining, out var version) && version == session.Version)
        {
            await session.ShowAsync(RemainingTimeFormatter.Format(remaining), cancellationToken);
        }

        session.Begin();
    }

    /// <summary>
    /// Ends every running countdown and restores each widget's configured appearance.
    /// Stop Sound halts the single active playback and has no widget of its own, so it resets
    /// whichever widgets were counting.
    /// </summary>
    public async Task ResetAllAsync(CancellationToken cancellationToken)
    {
        CountdownSession[] sessions;
        lock (_sync)
        {
            sessions = [.. _sessions.Values];
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            session.Stop();
        }

        foreach (var session in sessions)
        {
            await session.RestoreAsync(cancellationToken);
            session.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CountdownSession[] sessions;
        lock (_sync)
        {
            sessions = [.. _sessions.Values];
            _sessions.Clear();
            _widgets = null;
        }

        foreach (var session in sessions)
        {
            session.Stop();
            session.Dispose();
        }
    }

    private void OnSessionFinished(CountdownSession session)
    {
        lock (_sync)
        {
            if (_sessions.TryGetValue(session.WidgetId, out var current) && ReferenceEquals(current, session))
            {
                _sessions.Remove(session.WidgetId);
                session.Dispose();
            }
        }
    }

    /// <summary>
    /// One widget's countdown. Owns its cancellation source and update task, so stopping it
    /// halts that widget's updates and leaves every other widget untouched.
    /// </summary>
    private sealed class CountdownSession : IDisposable
    {
        private readonly string _widgetId;
        private readonly AudioManager _audioManager;
        private readonly IWidgetApi _widgets;
        private readonly ILogger _logger;
        private readonly Action<CountdownSession> _onFinished;
        private readonly CancellationTokenSource _cancellation = new();
        private string? _lastLabel;
        private int _restored;

        public CountdownSession(
            string widgetId,
            long version,
            AudioManager audioManager,
            IWidgetApi widgets,
            ILogger logger,
            Action<CountdownSession> onFinished)
        {
            _widgetId = widgetId;
            _audioManager = audioManager;
            _widgets = widgets;
            _logger = logger;
            _onFinished = onFinished;
            Version = version;
        }

        public string WidgetId => _widgetId;

        /// <summary>The playback this countdown is bound to.</summary>
        public long Version { get; }

        public void Begin() => _ = RunAsync();

        public void Stop() => _cancellation.Cancel();

        public async Task ShowAsync(string label, CancellationToken cancellationToken)
        {
            if (_cancellation.IsCancellationRequested)
            {
                return;
            }

            // Only push when the displayed second actually changes, so a 300 ms tick does not
            // turn into three host invocations per second.
            if (string.Equals(_lastLabel, label, StringComparison.Ordinal))
            {
                return;
            }

            if (await ApplyAsync(new WidgetAppearancePatch { Label = label }, [], cancellationToken))
            {
                _lastLabel = label;
            }
        }

        public async Task RestoreAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _restored, 1) != 0)
            {
                return;
            }

            // Clearing the label override drops back to the widget's configured text and icon.
            await ApplyAsync(new WidgetAppearancePatch(), [WidgetAppearanceProperty.Label], cancellationToken);
            _lastLabel = null;
        }

        public void Dispose()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                while (!_cancellation.IsCancellationRequested)
                {
                    await Task.Delay(UpdateInterval, _cancellation.Token).ConfigureAwait(false);

                    // A different playback version means this widget's sound was stopped,
                    // replaced, or reached its end. Looping never bumps the version, so a
                    // looping sound keeps counting down and wraps on its own.
                    if (_audioManager.PlaybackVersion != Version)
                    {
                        break;
                    }

                    // At the end of a stream the position reaches the length a moment before
                    // the playback is torn down. Skip the write rather than flashing 00:00;
                    // the version check on the following tick restores the widget.
                    if (_audioManager.TryGetRemainingPlaybackTime(out var remaining) && remaining > TimeSpan.Zero)
                    {
                        await ShowAsync(RemainingTimeFormatter.Format(remaining), _cancellation.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RestoreAsync(CancellationToken.None).ConfigureAwait(false);
            _onFinished(this);
        }

        private async Task<bool> ApplyAsync(
            WidgetAppearancePatch patch,
            IReadOnlyCollection<WidgetAppearanceProperty> clear,
            CancellationToken cancellationToken)
        {
            var request = new WidgetAppearanceRequest
            {
                WidgetId = _widgetId,
                Patch = patch,
                ClearProperties = clear
            };

            try
            {
                return await _widgets.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HostInvocationException or OperationCanceledException or ObjectDisposedException)
            {
                // The host can refuse, time out or be briefly unreachable. Logged at debug so a
                // failure episode does not flood the host's rate-limited log.
                _logger.Debug(exception, "Unable to update the SoundBox countdown for its widget.");
                return false;
            }
        }
    }
}
