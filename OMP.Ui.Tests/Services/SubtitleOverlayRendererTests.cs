using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OMP.Lib.Subtitle;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Services;

public class SubtitleOverlayRendererTests
{
    private static readonly Rect _videoRect = new(0, 0, 800, 450);

    [AvaloniaTheory]
    [InlineData(VerticalAlignment.Top)]
    [InlineData(VerticalAlignment.Center)]
    [InlineData(VerticalAlignment.Bottom)]
    public void Render_PlacesTextAccordingToZoneVerticalAlignment(VerticalAlignment alignment)
    {
        var zone = NewZone(alignment);
        var (canvas, renderer) = CreateRenderer();

        renderer.Render([Cue(zone.Id)], [zone], _videoRect);
        Dispatcher.UIThread.RunJobs();

        var container = Assert.Single(canvas.GetVisualDescendants().OfType<Border>());
        var text = Assert.IsType<TextBlock>(container.Child);

        Assert.True(text.Bounds.Height < container.Bounds.Height, "text should not fill the zone");

        var offset = text.Bounds.Y;
        var slack = container.Bounds.Height - text.Bounds.Height;

        switch (alignment)
        {
            case VerticalAlignment.Top:
                Assert.Equal(0, offset, 1);
                break;
            case VerticalAlignment.Center:
                Assert.Equal(slack / 2, offset, 1);
                break;
            case VerticalAlignment.Bottom:
                Assert.Equal(slack, offset, 1);
                break;
        }
    }

    [AvaloniaFact]
    public void Render_SizesContainerToZoneButLetsBackgroundHugTheText()
    {
        var zone = NewZone(VerticalAlignment.Bottom);
        var (canvas, renderer) = CreateRenderer();

        renderer.Render([Cue(zone.Id)], [zone], _videoRect);
        Dispatcher.UIThread.RunJobs();

        var container = Assert.Single(canvas.GetVisualDescendants().OfType<Border>());
        var text = Assert.IsType<TextBlock>(container.Child);

        Assert.Equal(zone.Width * _videoRect.Width, container.Width, 1);
        Assert.Equal(zone.Height * _videoRect.Height, container.Height, 1);

        Assert.Null(container.Background);
        Assert.IsType<SolidColorBrush>(text.Background);
        Assert.True(text.Bounds.Height < container.Bounds.Height);
    }

    [AvaloniaFact]
    public void Render_ZoneWithNoCues_HidesItsContainer()
    {
        var zone = NewZone(VerticalAlignment.Bottom);
        var (canvas, renderer) = CreateRenderer();

        renderer.Render([Cue(zone.Id)], [zone], _videoRect);
        Dispatcher.UIThread.RunJobs();

        renderer.Render([], [zone], _videoRect);
        Dispatcher.UIThread.RunJobs();

        var container = Assert.Single(canvas.GetVisualDescendants().OfType<Border>());
        Assert.False(container.IsVisible);
    }

    private static (Canvas Canvas, SubtitleOverlayRenderer Renderer) CreateRenderer()
    {
        var canvas = new Canvas { Width = _videoRect.Width, Height = _videoRect.Height };
        var window = new Window { Content = canvas, Width = _videoRect.Width, Height = _videoRect.Height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (canvas, new SubtitleOverlayRenderer(canvas));
    }

    private static SubtitleZone NewZone(VerticalAlignment alignment) => new()
    {
        Id = "zone-under-test",
        X = 0.1,
        Y = 0.1,
        Width = 0.8,
        Height = 0.5,
        VerticalAlignment = alignment
    };

    private static SubtitleCue Cue(string zoneId) =>
        new(zoneId, [new SubtitleLine([new SubtitleRun("Caption", Bold: false, Italic: false)])], 0, 5);
}
