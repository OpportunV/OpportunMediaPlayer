namespace OMP.Lib.Subtitle;

internal static class SubtitleTextParser
{
    public static IReadOnlyList<SubtitleLine> Parse(string rawText)
    {
        var lines = new List<SubtitleLine>();
        var currentRuns = new List<SubtitleRun>();
        var bold = false;
        var italic = false;
        var drawingMode = false;

        var i = 0;
        while (i < rawText.Length)
        {
            if (rawText[i] == '{')
            {
                var close = rawText.IndexOf('}', i + 1);
                var blockEnd = close < 0 ? rawText.Length : close;
                var blockContent = rawText[(i + 1)..blockEnd];
                ApplyOverrideBlock(blockContent, ref bold, ref italic, ref drawingMode);
                i = close < 0 ? rawText.Length : close + 1;
                continue;
            }

            if (IsHardLineBreak(rawText, i))
            {
                lines.Add(new SubtitleLine(currentRuns));
                currentRuns = [];
                i += 2;
                continue;
            }

            var segmentEnd = FindNextTokenStart(rawText, i);
            var segment = rawText[i..segmentEnd];
            if (!drawingMode && segment.Length > 0)
            {
                AppendRun(currentRuns, segment, bold, italic);
            }

            i = segmentEnd;
        }

        if (currentRuns.Count > 0 || lines.Count == 0)
        {
            lines.Add(new SubtitleLine(currentRuns));
        }

        return lines;
    }

    private static void ApplyOverrideBlock(string blockContent, ref bool bold, ref bool italic, ref bool drawingMode)
    {
        foreach (var rawToken in blockContent.Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = rawToken.Trim();

            switch (token)
            {
                case "b0":
                    bold = false;
                    break;
                case "b1":
                    bold = true;
                    break;
                case "i0":
                    italic = false;
                    break;
                case "i1":
                    italic = true;
                    break;
                default:
                    var drawingMatch = SubtitleRegex.DrawingModeTag().Match(token);
                    if (drawingMatch.Success)
                    {
                        drawingMode = drawingMatch.Groups[1].Value != "0";
                    }

                    break;
            }
        }
    }

    private static void AppendRun(List<SubtitleRun> runs, string text, bool bold, bool italic)
    {
        if (runs.Count > 0)
        {
            var last = runs[^1];
            if (last.Bold == bold && last.Italic == italic)
            {
                runs[^1] = last with { Text = last.Text + text };
                return;
            }
        }

        runs.Add(new SubtitleRun(text, bold, italic));
    }

    private static bool IsHardLineBreak(string text, int index)
    {
        return index + 1 < text.Length && text[index] == '\\' && text[index + 1] == 'N';
    }

    private static int FindNextTokenStart(string text, int from)
    {
        for (var i = from; i < text.Length; i++)
        {
            if (text[i] == '{' || IsHardLineBreak(text, i))
            {
                return i;
            }
        }

        return text.Length;
    }
}
