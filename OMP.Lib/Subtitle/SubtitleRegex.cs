using System.Text.RegularExpressions;

namespace OMP.Lib.Subtitle;

internal static partial class SubtitleRegex
{
    [GeneratedRegex(@"^p(\d+)$")]
    public static partial Regex DrawingModeTag();
}
