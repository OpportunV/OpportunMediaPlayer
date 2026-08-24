using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using OMP.Lib.Subtitle;
using OMP.Ui.Helpers;
using OMP.Ui.Settings;

namespace OMP.Ui.Services;

internal sealed class SubtitleOverlayRenderer(Canvas overlayCanvas)
{
    private readonly Dictionary<string, Border> _zoneContainers = [];

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
                var container = GetOrCreateContainer(zone.Id);
                PositionAndStyle(container, zone, videoContentRect);
                SetCueContent(TextOf(container), group.ToList());
                container.IsVisible = true;
            }
        }

        foreach (var (zoneId, container) in _zoneContainers)
        {
            if (!activeZoneIds.Contains(zoneId))
            {
                container.IsVisible = false;
            }
        }
    }

    public void Clear()
    {
        foreach (var container in _zoneContainers.Values)
        {
            container.IsVisible = false;
        }
    }

    private static TextBlock TextOf(Border container) => (TextBlock)container.Child!;

    private Border GetOrCreateContainer(string zoneId)
    {
        if (_zoneContainers.TryGetValue(zoneId, out var existing))
        {
            return existing;
        }

        var container = new Border { Child = new TextBlock { TextWrapping = TextWrapping.Wrap } };
        _zoneContainers[zoneId] = container;
        overlayCanvas.Children.Add(container);
        return container;
    }

    private static void PositionAndStyle(Border container, SubtitleZone zone, Rect videoRect)
    {
        Canvas.SetLeft(container, videoRect.X + zone.X * videoRect.Width);
        Canvas.SetTop(container, videoRect.Y + zone.Y * videoRect.Height);
        container.Width = zone.Width * videoRect.Width;
        container.Height = zone.Height * videoRect.Height;

        SubtitleZoneTextStyle.Apply(TextOf(container), zone, videoRect.Height);
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
