using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OMP.Ui.Settings;

namespace OMP.Ui.Helpers;

internal static class SubtitleZoneTextStyle
{
    private const double MinFontSize = 6;

    /// <summary>
    /// Applies a zone's text appearance to <paramref name="textBlock"/>. Shared by the live overlay
    /// and the zone editor's preview: the editor exists to show what playback will look like, so any
    /// drift between two copies of this would make the preview silently lie.
    /// <paramref name="referenceHeight"/> is the height the zone's font-size ratio is relative to -
    /// the video content rect on the overlay, the preview canvas in the editor.
    /// </summary>
    public static void Apply(TextBlock textBlock, SubtitleZone zone, double referenceHeight)
    {
        textBlock.FontFamily = new FontFamily(zone.FontFamily);
        textBlock.FontSize = Math.Max(MinFontSize, zone.FontSizeRatio * referenceHeight);
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
}
