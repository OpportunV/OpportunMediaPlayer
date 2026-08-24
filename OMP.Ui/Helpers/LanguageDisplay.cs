using System.Globalization;

namespace OMP.Ui.Helpers;

internal static class LanguageDisplay
{
    /// <summary>
    /// The language's own native name, or the code itself when it names no known culture -
    /// yt-dlp reports whatever the source labelled a track with, which is not always a valid tag.
    /// </summary>
    public static string NativeName(string languageCode)
    {
        try
        {
            return CultureInfo.GetCultureInfo(languageCode).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return languageCode;
        }
    }
}
