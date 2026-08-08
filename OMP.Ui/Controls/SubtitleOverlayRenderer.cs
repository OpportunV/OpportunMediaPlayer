using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using OMP.Lib.Subtitle;
using OMP.Ui.Settings;

namespace OMP.Ui.Controls;

internal sealed class SubtitleOverlayRenderer(Canvas overlayCanvas)
{
    private readonly Dictionary<string, TextBlock> _zoneTextBlocks = [];

    public void Render(IReadOnlyList<SubtitleCue> cues, IReadOnlyList<SubtitleZone> zones, Rect videoContentRect)
    {
        var activeZoneIds = new HashSet<string>();

        if (videoContentRect is { Width: > 0, Height: > 0 })
        {
            foreach (var group in cues.GroupBy(cue => cue.ZoneId))
            {
                var zone = zones.FirstOrDefault(z => z.Id == group.Key);
                if (zone is null)
                {
                    continue;
                }

                activeZoneIds.Add(zone.Id);
                var textBlock = GetOrCreateTextBlock(zone.Id);
                PositionAndStyle(textBlock, zone, videoContentRect);
                SetCueContent(textBlock, group.ToList());
                textBlock.IsVisible = true;
            }
        }

        foreach (var (zoneId, textBlock) in _zoneTextBlocks)
        {
            if (!activeZoneIds.Contains(zoneId))
            {
                textBlock.IsVisible = false;
            }
        }
    }

    public void Clear()
    {
        foreach (var textBlock in _zoneTextBlocks.Values)
        {
            textBlock.IsVisible = false;
        }
    }

    private TextBlock GetOrCreateTextBlock(string zoneId)
    {
        if (_zoneTextBlocks.TryGetValue(zoneId, out var existing))
        {
            return existing;
        }

        var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
        _zoneTextBlocks[zoneId] = textBlock;
        overlayCanvas.Children.Add(textBlock);
        return textBlock;
    }

    private static void PositionAndStyle(TextBlock textBlock, SubtitleZone zone, Rect videoRect)
    {
        Canvas.SetLeft(textBlock, videoRect.X + zone.X * videoRect.Width);
        Canvas.SetTop(textBlock, videoRect.Y + zone.Y * videoRect.Height);
        textBlock.Width = zone.Width * videoRect.Width;
        textBlock.Height = zone.Height * videoRect.Height;

        textBlock.FontFamily = new FontFamily(zone.FontFamily);
        textBlock.FontSize = Math.Max(6, zone.FontSizeRatio * videoRect.Height);
        textBlock.Foreground = new SolidColorBrush(Color.Parse(zone.FontColor));
        textBlock.Background = new SolidColorBrush(Color.Parse(zone.BackgroundColor), zone.BackgroundOpacity);
        textBlock.TextAlignment = zone.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => TextAlignment.Left,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Center
        };
        textBlock.HorizontalAlignment = zone.HorizontalAlignment;
        textBlock.VerticalAlignment = zone.VerticalAlignment;
    }

    private static void SetCueContent(TextBlock textBlock, List<SubtitleCue> cues)
    {
        var inlines = new InlineCollection();

        for (var cueIndex = 0; cueIndex < cues.Count; cueIndex++)
        {
            if (cueIndex > 0)
            {
                inlines.Add(new LineBreak());
            }

            var lines = cues[cueIndex].Lines;
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                if (lineIndex > 0)
                {
                    inlines.Add(new LineBreak());
                }

                foreach (var run in lines[lineIndex].Runs)
                {
                    inlines.Add(new Run(run.Text)
                    {
                        FontWeight = run.Bold ? FontWeight.Bold : FontWeight.Normal,
                        FontStyle = run.Italic ? FontStyle.Italic : FontStyle.Normal
                    });
                }
            }
        }

        textBlock.Inlines = inlines;
    }
}
