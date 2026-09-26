namespace OMP.Lib.Session;

/// <summary>
/// The playback clock is wall-clock driven and keeps ticking until the session notices the end of
/// the media. When that detection lags (or never fires, e.g. a network source that never delivers a
/// clean end-of-file), the position must still never be reported past the duration, and the
/// session needs a time-based backstop to end playback on its own.
/// </summary>
internal static class PlaybackPosition
{
    public static double ClampToDuration(double clockSeconds, double durationSeconds) =>
        durationSeconds > 0 ? Math.Min(clockSeconds, durationSeconds) : clockSeconds;

    public static bool HasOverrunDuration(double clockSeconds, double durationSeconds, double graceSeconds) =>
        durationSeconds > 0 && clockSeconds >= durationSeconds + graceSeconds;
}
