using System.Globalization;

namespace SoundBox.Playback;

/// <summary>
/// Formats a remaining playback duration for display. Shared by the widget countdown
/// and the soundbox_playback_remaining variable so both always agree.
/// </summary>
public static class RemainingTimeFormatter
{
    public const string Zero = "00:00";

    /// <summary>
    /// Formats a duration as MM:SS, widening to HH:MM:SS once it reaches an hour.
    /// Rounds up so a sound in its final fraction of a second still reads 00:01
    /// and only reports 00:00 once there is genuinely nothing left.
    /// </summary>
    public static string Format(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return Zero;
        }

        var totalSeconds = (long)Math.Ceiling(remaining.TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;

        return hours > 0
            ? FormattableString.Invariant($"{hours:00}:{minutes:00}:{seconds:00}")
            : FormattableString.Invariant($"{minutes:00}:{seconds:00}");
    }
}
