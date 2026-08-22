using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using OMP.Lib.Session;

namespace OMP.Ui.Helpers;

internal static class YtDlpFormatSelector
{
    private const string NoLanguageGroupKey = "__no_language__";

    private const int MaxPreferredHeight = 1080;

    public static YtDlpMediaSelection? SelectMediaSources(JsonDocument document)
    {
        var root = document.RootElement;
        var title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;

        var formats = root.TryGetProperty("formats", out var formatsElement) &&
                      formatsElement.ValueKind == JsonValueKind.Array
            ? formatsElement.EnumerateArray().ToList()
            : [];

        var primary = SelectPrimaryFormat(root, formats);
        if (primary is not { } chosenPrimary || !chosenPrimary.TryGetProperty("url", out var urlElement))
        {
            return null;
        }

        var audioSidecars = SelectAudioSidecars(formats, chosenPrimary, urlElement.GetString());

        return new YtDlpMediaSelection(urlElement.GetString()!, title, audioSidecars, ExtractHeaders(chosenPrimary));
    }

    private static JsonElement? SelectPrimaryFormat(JsonElement root, List<JsonElement> formats)
    {
        if (IsProgressive(root) && IsWithinResolutionCap(root) && root.TryGetProperty("url", out _))
        {
            return root;
        }

        return SelectBest(formats, f => IsProgressive(f) && IsWithinResolutionCap(f))
            ?? SelectBest(formats, f => IsVideoOnly(f) && IsWithinResolutionCap(f))
            ?? SelectBest(formats, IsProgressive)
            ?? SelectBest(formats, IsVideoOnly)
            ?? SelectBest(formats, IsAudioOnly);
    }

    private static bool IsWithinResolutionCap(JsonElement format) =>
        !format.TryGetProperty("height", out var height) ||
        height.ValueKind != JsonValueKind.Number ||
        height.GetDouble() <= MaxPreferredHeight;

    private static IReadOnlyList<AudioSidecarSource> SelectAudioSidecars(
        List<JsonElement> formats, JsonElement primary, string? primaryUrl)
    {
        var primaryHasAudio = HasAudio(primary);

        var bestPerGroup = new Dictionary<string, JsonElement>();
        var bestScorePerGroup = new Dictionary<string, double>();

        foreach (var format in formats)
        {
            if (!IsAudioOnly(format) || !format.TryGetProperty("url", out var urlElement))
            {
                continue;
            }

            var url = urlElement.GetString();
            if (string.IsNullOrEmpty(url) || url == primaryUrl)
            {
                continue;
            }

            var language = format.TryGetProperty("language", out var languageElement) ? languageElement.GetString() : null;
            if (language is null && primaryHasAudio)
            {
                continue;
            }

            var groupKey = language ?? NoLanguageGroupKey;
            var score = GetScore(format);

            if (!bestScorePerGroup.TryGetValue(groupKey, out var bestScore) || score > bestScore)
            {
                bestScorePerGroup[groupKey] = score;
                bestPerGroup[groupKey] = format;
            }
        }

        return bestPerGroup.Values
            .OrderByDescending(GetLanguagePreference)
            .Select(format =>
            {
                var url = format.GetProperty("url").GetString()!;
                var language = format.TryGetProperty("language", out var languageElement) ? languageElement.GetString() : null;
                return new AudioSidecarSource(url, language, DescribeLanguage(format, language), ExtractHeaders(format));
            })
            .ToList();
    }

    private static int GetLanguagePreference(JsonElement format) =>
        format.TryGetProperty("language_preference", out var preference) && preference.ValueKind == JsonValueKind.Number
            ? preference.GetInt32()
            : -1;

    private static string? DescribeLanguage(JsonElement format, string? languageCode)
    {
        if (format.TryGetProperty("format_note", out var noteElement) && noteElement.GetString() is { Length: > 0 } note)
        {
            return note;
        }

        if (languageCode is null)
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageCode).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return languageCode;
        }
    }

    private static IReadOnlyDictionary<string, string>? ExtractHeaders(JsonElement format)
    {
        if (!format.TryGetProperty("http_headers", out var headersElement) ||
            headersElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var headers = new Dictionary<string, string>();
        foreach (var property in headersElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is { } value)
            {
                headers[property.Name] = value;
            }
        }

        return headers.Count > 0 ? headers : null;
    }

    private static JsonElement? SelectBest(List<JsonElement> formats, Func<JsonElement, bool> predicate)
    {
        JsonElement? best = null;
        var bestScore = -1.0;

        foreach (var format in formats)
        {
            if (!predicate(format) || !format.TryGetProperty("url", out _))
            {
                continue;
            }

            var score = GetScore(format);
            if (score > bestScore)
            {
                bestScore = score;
                best = format;
            }
        }

        return best;
    }

    private static bool IsProgressive(JsonElement element) => HasVideo(element) && HasAudio(element);

    private static bool IsVideoOnly(JsonElement element) => HasVideo(element) && !HasAudio(element);

    private static bool IsAudioOnly(JsonElement element) => !HasVideo(element) && HasAudio(element);

    private static bool HasVideo(JsonElement element) =>
        element.TryGetProperty("vcodec", out var vcodec) && vcodec.GetString() is { } v && v != "none";

    private static bool HasAudio(JsonElement element) =>
        element.TryGetProperty("acodec", out var acodec) && acodec.GetString() is { } a && a != "none";

    private static double GetScore(JsonElement format)
    {
        if (format.TryGetProperty("height", out var height) && height.ValueKind == JsonValueKind.Number)
        {
            return height.GetDouble();
        }

        return format.TryGetProperty("tbr", out var tbr) && tbr.ValueKind == JsonValueKind.Number
            ? tbr.GetDouble()
            : 0;
    }
}
