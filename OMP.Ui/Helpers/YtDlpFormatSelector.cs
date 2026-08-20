using System.Text.Json;

namespace OMP.Ui.Helpers;

internal static class YtDlpFormatSelector
{
    public static (string Url, string? Title)? SelectPlayableFormat(JsonDocument document)
    {
        var root = document.RootElement;
        var title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;

        if (IsProgressive(root) && root.TryGetProperty("url", out var topLevelUrl) && topLevelUrl.GetString() is { } url)
        {
            return (url, title);
        }

        if (!root.TryGetProperty("formats", out var formats) || formats.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? best = null;
        var bestScore = -1.0;

        foreach (var format in formats.EnumerateArray())
        {
            if (!IsProgressive(format) || !format.TryGetProperty("url", out _))
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

        return best is { } chosen ? (chosen.GetProperty("url").GetString()!, title) : null;
    }

    private static bool IsProgressive(JsonElement element) =>
        element.TryGetProperty("vcodec", out var vcodec) && vcodec.GetString() is { } v && v != "none" &&
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
