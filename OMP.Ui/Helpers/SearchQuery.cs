using System;
using System.Globalization;

namespace OMP.Ui.Helpers;

internal static class SearchQuery
{
    private const CompareOptions MatchOptions = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>
    /// True when every whitespace-separated word of <paramref name="query"/> appears somewhere in
    /// <paramref name="text"/>, ignoring case and accents ("francais" finds "Français"). An empty
    /// query matches everything.
    /// </summary>
    public static bool Matches(string? query, string text)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
        foreach (var word in query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (compareInfo.IndexOf(text, word, MatchOptions) < 0)
            {
                return false;
            }
        }

        return true;
    }
}
