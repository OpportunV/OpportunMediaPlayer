using System.Globalization;

namespace OMP.Ui.Helpers;

internal static class PlaybackSpeedFormat
{
    public static string Format(double speed) => speed % 1 == 0
        ? speed.ToString("0", CultureInfo.InvariantCulture) + "x"
        : speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";
}
