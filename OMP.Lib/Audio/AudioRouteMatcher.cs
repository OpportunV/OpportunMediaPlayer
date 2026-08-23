using System.Globalization;
using OMP.Lib.Audio.Output;

namespace OMP.Lib.Audio;

public static class AudioRouteMatcher
{
    private const string UnknownLanguageTag = "und";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> _threeToTwoLetterLanguageCodes =
        new(BuildThreeToTwoLetterLanguageCodeMap);

    public static IReadOnlyList<AudioRoute> Match(
        IReadOnlyList<AudioStream> streams,
        IReadOnlyList<AudioOutput> outputs,
        IReadOnlyList<PreferredAudioTrack> preferred)
    {
        var availableStreams = streams.ToList();
        var routes = new List<AudioRoute>();
        var unmatchedOutputs = new List<AudioOutput>();

        foreach (var pref in preferred)
        {
            var output = outputs.FirstOrDefault(o => o.FriendlyName == pref.OutputFriendlyName);
            if (output is null)
            {
                continue;
            }

            var match = availableStreams.FirstOrDefault(s =>
                !IsUnknown(s.Title) && !IsUnknown(s.Language) &&
                s.Title == pref.Title && LanguagesMatch(s.Language, pref.Language));

            match ??= availableStreams.FirstOrDefault(s => !IsUnknown(s.Language) && LanguagesMatch(s.Language, pref.Language));

            if (match is not null)
            {
                routes.Add(new AudioRoute(match, output));
                availableStreams.Remove(match);
            }
            else
            {
                unmatchedOutputs.Add(output);
            }
        }

        foreach (var (output, stream) in unmatchedOutputs.Zip(availableStreams))
        {
            routes.Add(new AudioRoute(stream, output));
        }

        return routes;
    }

    private static bool IsUnknown(string value) =>
        value.Equals("Unknown", StringComparison.Ordinal) ||
        value.Equals(UnknownLanguageTag, StringComparison.OrdinalIgnoreCase);

    private static bool LanguagesMatch(string a, string b) =>
        NormalizeLanguage(a).Equals(NormalizeLanguage(b), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeLanguage(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language).TwoLetterISOLanguageName;
        }
        catch (CultureNotFoundException)
        {
            return _threeToTwoLetterLanguageCodes.Value.GetValueOrDefault(language, language);
        }
    }

    private static Dictionary<string, string> BuildThreeToTwoLetterLanguageCodeMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (culture.ThreeLetterISOLanguageName.Length > 0)
            {
                map.TryAdd(culture.ThreeLetterISOLanguageName, culture.TwoLetterISOLanguageName);
            }
        }

        return map;
    }
}
