using System.Text.Json;
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
/// to is replaced, stopped or finishes, at which point the label captured before the countdown
/// is written back to the same widget state.
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
        IWidgetApi widgets;
        long version;
        CountdownSession? existing;
        lock (_sync)
        {
            if (Volatile.Read(ref _disposed) != 0 || _widgets is null)
            {
                return;
            }

            widgets = _widgets;
            version = _audioManager.PlaybackVersion;
            _sessions.TryGetValue(widgetId, out existing);
        }

        var widgetSnapshot = await GetWidgetSnapshotAsync(widgets, widgetId, existing, cancellationToken).ConfigureAwait(false);
        if (widgetSnapshot is null || _audioManager.PlaybackVersion != version)
        {
            return;
        }

        CountdownSession? superseded;
        CountdownSession session;

        lock (_sync)
        {
            if (Volatile.Read(ref _disposed) != 0
                || !ReferenceEquals(_widgets, widgets)
                || _audioManager.PlaybackVersion != version)
            {
                return;
            }

            _sessions.Remove(widgetId, out superseded);

            session = new CountdownSession(
                widgetId,
                version,
                widgetSnapshot.StateIds,
                widgetSnapshot.Label,
                _audioManager,
                widgets,
                _logger,
                OnSessionFinished);
            _sessions[widgetId] = session;
        }

        if (superseded is not null)
        {
            superseded.Stop();
            if (!superseded.StateIds.SequenceEqual(session.StateIds))
            {
                await superseded.RestoreAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        // Show the full duration straight away instead of waiting for the first tick.
        if (_audioManager.TryGetRemainingPlaybackTime(out var remaining, out var playbackVersion) && playbackVersion == session.Version)
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

    private async Task<WidgetSnapshot?> GetWidgetSnapshotAsync(
        IWidgetApi widgets,
        string widgetId,
        CountdownSession? existing,
        CancellationToken cancellationToken)
    {
        var widget = widgets.GetWidgets().FirstOrDefault(candidate =>
            string.Equals(candidate.Id, widgetId, StringComparison.Ordinal));

        if (widget is null)
        {
            _logger.Warning("Unable to start the countdown because widget {WidgetId} is not in the host widget cache.", widgetId);
            return null;
        }

        var stateIds = GetStateIds(widget);
        if (existing is not null && existing.StateIds.SequenceEqual(stateIds))
        {
            return new WidgetSnapshot(stateIds, existing.OriginalLabel);
        }

        var stateId = widget.CurrentStateId
            ?? (widget.States.Count > 0 ? widget.States[0].Id : null);

        try
        {
            var data = await widgets.GetWidgetDataAsync(widgetId, cancellationToken).ConfigureAwait(false);
            if (data is not null)
            {
                return new WidgetSnapshot(stateIds, ReadConfiguredLabel(data.Data, stateId));
            }
        }
        catch (HostInvocationException exception)
        {
            _logger.Warning(exception, "Unable to read the configured label for widget {WidgetId}.", widgetId);
        }

        if (widget.States.Count > 1)
        {
            _logger.Warning("Skipping the countdown for widget {WidgetId} because its active label could not be read.", widgetId);
            return null;
        }

        // The host uses its type name as a descriptor label when an action button has no text.
        var label = IsDefaultActionButtonLabel(widget) ? null : widget.Label;
        return new WidgetSnapshot(stateIds, label);
    }

    private static bool IsDefaultActionButtonLabel(WidgetTargetInfo widget) =>
        string.Equals(widget.Type, "ActionButton", StringComparison.Ordinal)
        && string.Equals(widget.Label, "Button", StringComparison.Ordinal);

    private static IReadOnlyCollection<string> GetStateIds(WidgetTargetInfo widget)
    {
        if (widget.CurrentStateId is { } currentStateId
            && widget.States.Any(state => string.Equals(state.Id, currentStateId, StringComparison.Ordinal)))
        {
            return [currentStateId];
        }

        return [WidgetStates.Current];
    }

    private static string? ReadConfiguredLabel(JsonElement data, string? stateId)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (data.TryGetProperty("states", out var states))
        {
            if (!string.IsNullOrEmpty(stateId) && states.ValueKind == JsonValueKind.Array)
            {
                foreach (var state in states.EnumerateArray())
                {
                    if (state.ValueKind == JsonValueKind.Object
                        && state.TryGetProperty("id", out var id)
                        && id.ValueKind == JsonValueKind.String
                        && string.Equals(id.GetString(), stateId, StringComparison.Ordinal))
                    {
                        return ReadStateLabel(state);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(stateId)
                && states.ValueKind == JsonValueKind.Object
                && states.TryGetProperty(stateId, out var state))
            {
                return ReadStateLabel(state);
            }

            return null;
        }

        return data.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String
            ? label.GetString()
            : null;
    }

    private static string? ReadStateLabel(JsonElement state)
    {
        if (state.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (state.TryGetProperty("appearance", out var appearance)
            && appearance.ValueKind == JsonValueKind.Object)
        {
            return appearance.TryGetProperty("label", out var label) && label.ValueKind == JsonValueKind.String
                ? label.GetString()
                : null;
        }

        return state.TryGetProperty("label", out var legacyLabel) && legacyLabel.ValueKind == JsonValueKind.String
            ? legacyLabel.GetString()
            : null;
    }

    /// <summary>
    /// One widget's countdown. Owns its cancellation source and update task, so stopping it
    /// halts that widget's updates and leaves every other widget untouched.
    /// </summary>
    private sealed class CountdownSession : IDisposable
    {
        private readonly string _widgetId;
        private readonly IReadOnlyCollection<string> _stateIds;
        private readonly string? _originalLabel;
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
            IReadOnlyCollection<string> stateIds,
            string? originalLabel,
            AudioManager audioManager,
            IWidgetApi widgets,
            ILogger logger,
            Action<CountdownSession> onFinished)
        {
            _widgetId = widgetId;
            _stateIds = stateIds;
            _originalLabel = originalLabel;
            _audioManager = audioManager;
            _widgets = widgets;
            _logger = logger;
            _onFinished = onFinished;
            Version = version;
        }

        public string WidgetId => _widgetId;
        public IReadOnlyCollection<string> StateIds => _stateIds;
        public string? OriginalLabel => _originalLabel;

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

            var patch = new WidgetAppearancePatch { Label = _originalLabel ?? string.Empty };
            await ApplyAsync(patch, [], cancellationToken);
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
                StateIds = _stateIds,
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

    private sealed record WidgetSnapshot(IReadOnlyCollection<string> StateIds, string? Label);
}
