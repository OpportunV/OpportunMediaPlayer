using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using OMP.Ui.Helpers;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Helpers;

public class SubtitleZoneTextStyleTests
{
    [AvaloniaFact]
    public void Apply_ScalesFontSizeByReferenceHeight()
    {
        var textBlock = new TextBlock();
        var zone = new SubtitleZone { FontSizeRatio = 0.05 };

        SubtitleZoneTextStyle.Apply(textBlock, zone, 800);

        Assert.Equal(40, textBlock.FontSize);
    }

    [AvaloniaFact]
    public void Apply_TinyReferenceHeight_ClampsFontSizeToMinimum()
    {
        var textBlock = new TextBlock();
        var zone = new SubtitleZone { FontSizeRatio = 0.05 };

        SubtitleZoneTextStyle.Apply(textBlock, zone, 10);

        Assert.Equal(6, textBlock.FontSize);
    }

    [AvaloniaTheory]
    [InlineData(HorizontalAlignment.Left, TextAlignment.Left)]
    [InlineData(HorizontalAlignment.Right, TextAlignment.Right)]
    [InlineData(HorizontalAlignment.Center, TextAlignment.Center)]
    [InlineData(HorizontalAlignment.Stretch, TextAlignment.Center)]
    public void Apply_MapsHorizontalAlignmentToTextAlignment(
        HorizontalAlignment alignment, TextAlignment expected)
    {
        var textBlock = new TextBlock();
        var zone = new SubtitleZone { HorizontalAlignment = alignment };

        SubtitleZoneTextStyle.Apply(textBlock, zone, 500);

        Assert.Equal(expected, textBlock.TextAlignment);
        Assert.Equal(alignment, textBlock.HorizontalAlignment);
    }

    [AvaloniaFact]
    public void Apply_UsesZoneColorsAndBackgroundOpacity()
    {
        var textBlock = new TextBlock();
        var zone = new SubtitleZone
        {
            FontFamily = "Consolas",
            FontColor = "#FF0000",
            BackgroundColor = "#0000FF",
            BackgroundOpacity = 0.25,
            VerticalAlignment = VerticalAlignment.Top
        };

        SubtitleZoneTextStyle.Apply(textBlock, zone, 500);

        Assert.Equal("Consolas", textBlock.FontFamily.Name);
        Assert.Equal(Colors.Red, Assert.IsType<SolidColorBrush>(textBlock.Foreground).Color);

        var background = Assert.IsType<SolidColorBrush>(textBlock.Background);
        Assert.Equal(Colors.Blue, background.Color);
        Assert.Equal(0.25, background.Opacity);

        Assert.Equal(VerticalAlignment.Top, textBlock.VerticalAlignment);
    }
}
