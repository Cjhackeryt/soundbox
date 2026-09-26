using MacroDeck.Sdk.Variables;
using SoundBox.Audio;
using SoundBox.Playback;

namespace SoundBox.Variables;

/// <summary>
/// Exposes the time left in SoundBox's active playback as a Macro Deck text variable.
/// Superseded by the per-widget countdown on the Play Sound action, which needs no
/// configuration; this is kept for configurations that already reference the variable.
/// The value is derived from the audio engine on every read, so it needs no background
/// timer and cannot keep counting after a stop, a restart or a natural finish.
/// </summary>
public sealed class PlaybackRemainingVariableProvider : IVariableProvider
{
    public const string PlaybackRemainingName = "soundbox_playback_remaining";

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(300);

    private readonly AudioManager _audioManager;
    private readonly IReadOnlyList<VariableDefinition> _variables;
    private readonly VariableDefinition _playbackRemaining;

    public PlaybackRemainingVariableProvider(AudioManager audioManager)
    {
        _audioManager = audioManager;
        _playbackRemaining = VariableDefinition.Eager(
            PlaybackRemainingName,
            VariableType.Text,
            decimalPlaces: null,
            refreshInterval: RefreshInterval) with
        {
            DisplayName = Strings.Variables.PlaybackRemaining.DisplayName(),
            Description = Strings.Variables.PlaybackRemaining.Description()
        };
        _variables = [_playbackRemaining];
    }

    public IReadOnlyList<VariableDefinition> Variables => _variables;

    public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(localId, _playbackRemaining.ResolvedId, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(VariableReading.Unavailable);
        }

        _audioManager.TryGetRemainingPlaybackTime(out var remaining);
        return ValueTask.FromResult(VariableReading.Of(RemainingTimeFormatter.Format(remaining)));
    }
}
