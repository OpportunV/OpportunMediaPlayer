namespace OMP.Ui.Extensions;

internal static class PlaybackSpeedFormat
{
    public static string Format(double speed) => speed % 1 == 0 ? $"{speed:0}x" : $"{speed:0.##}x";
}
