using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;
using OMP.Ui.Services;
using OMP.Ui.Tests.TestDoubles;

namespace OMP.Ui.Tests.Services;

/// <summary>
/// The tick itself is driven by a <c>DispatcherTimer</c>, so these pump the dispatcher rather than
/// waiting on wall-clock time. What matters is the state the tick shares with the slider and the
/// subtitles toggle - three things that used to be loose fields on the window.
/// </summary>
public class PlaybackStatusTimerTests
{
    private static readonly SubtitleStream _english = new(1, "subrip", "English", "en", IsTextBased: true);

    [AvaloniaFact]
    public void Tick_AdvancesTheSliderAndReadout()
    {
        var h = new Harness();
        h.Session.CurrentTime = TimeSpan.FromSeconds(42);

        h.PumpTick();

        Assert.Equal(42, h.Slider.Value, 3);
        Assert.Equal(TimeSpan.FromSeconds(42).Format(), h.Label.Text);
    }

    [AvaloniaFact]
    public void Tick_WithNoSession_LeavesTheSliderAlone()
    {
        var h = new Harness(withSession: false);
        h.Slider.Value = 17;

        h.PumpTick();

        Assert.Equal(17, h.Slider.Value, 3);
    }

    /// <summary>
    /// While dragging, the tick must not fight the user for the slider position.
    /// </summary>
    [AvaloniaFact]
    public void Tick_WhileDragging_DoesNotMoveTheSlider()
    {
        var h = new Harness();
        h.PressSlider();
        var whileDragging = h.Slider.Value;

        h.Session.CurrentTime = TimeSpan.FromSeconds(42);
        h.PumpTick();

        Assert.Equal(whileDragging, h.Slider.Value, 3);
        Assert.NotEqual(42, h.Slider.Value, 3);
    }

    [AvaloniaFact]
    public void ReleasingTheSlider_SeeksToWhereItWasDroppedAndResumesFollowing()
    {
        var h = new Harness();
        h.PressSlider();
        var dropped = h.Slider.Value;

        h.ReleaseSlider();

        Assert.True(dropped > 0, "pressing the slider should move it off zero");
        Assert.Equal(TimeSpan.FromSeconds(dropped), h.Session.LastSeekTarget);

        h.Session.CurrentTime = TimeSpan.FromSeconds(55);
        h.PumpTick();
        Assert.Equal(55, h.Slider.Value, 3);
    }

    /// <summary>
    /// Routing a subtitle track is the request to see subtitles, so the 0 to non-zero transition
    /// turns them on by itself.
    /// </summary>
    [AvaloniaFact]
    public void Tick_WhenTheFirstSubtitleRouteAppears_EnablesSubtitles()
    {
        var h = new Harness();
        h.PumpTick();
        Assert.False(h.SubtitlesButton.IsChecked);
        Assert.Equal(0, h.OverlayRenderCount);

        h.Session.SubtitleRoutes = [new SubtitleRoute(_english, "zone-a")];
        h.PumpTick();

        Assert.True(h.SubtitlesButton.IsChecked);
        Assert.True(h.OverlayRenderCount > 0);
    }

    [AvaloniaFact]
    public void UncheckingSubtitles_ClearsTheOverlayAndStopsRendering()
    {
        var h = new Harness();
        h.Session.SubtitleRoutes = [new SubtitleRoute(_english, "zone-a")];
        h.PumpTick();

        h.SubtitlesButton.IsChecked = false;
        var rendersBefore = h.OverlayRenderCount;
        h.PumpTick();

        Assert.Equal(1, h.OverlayClearCount);
        Assert.Equal(rendersBefore, h.OverlayRenderCount);
    }

    [AvaloniaFact]
    public void ResetForNewSession_ReArmsTheAutoEnableRule()
    {
        var h = new Harness();
        h.Session.SubtitleRoutes = [new SubtitleRoute(_english, "zone-a")];
        h.PumpTick();
        Assert.True(h.SubtitlesButton.IsChecked);

        h.Timer.ResetForNewSession();

        Assert.False(h.SubtitlesButton.IsChecked);

        h.PumpTick();
        Assert.True(h.SubtitlesButton.IsChecked);
    }

    [AvaloniaFact]
    public void Dispose_UnsubscribesFromTheSubtitlesToggle()
    {
        var h = new Harness();
        h.Session.SubtitleRoutes = [new SubtitleRoute(_english, "zone-a")];
        h.PumpTick();

        h.Timer.Dispose();
        h.SubtitlesButton.IsChecked = false;

        Assert.Equal(0, h.OverlayClearCount);
    }

    [AvaloniaFact]
    public void ResizingTheVideoSurface_RedrawsTheOverlayImmediately()
    {
        var h = new Harness();
        h.Session.SubtitleRoutes = [new SubtitleRoute(_english, "zone-a")];
        h.PumpTick();
        var before = h.OverlayRenderCount;

        h.ResizeVideoSurface(640, 360);

        Assert.True(h.OverlayRenderCount > before, "resizing should redraw without waiting for a tick");
    }

    [AvaloniaFact]
    public void ResizingTheVideoSurface_WithSubtitlesOff_DrawsNothing()
    {
        var h = new Harness();
        var before = h.OverlayRenderCount;

        h.ResizeVideoSurface(640, 360);

        Assert.Equal(before, h.OverlayRenderCount);
    }

    private sealed class Harness
    {
        public Slider Slider { get; } = new() { Minimum = 0, Maximum = 100 };

        public TextBlock Label { get; } = new();

        public ToggleButton SubtitlesButton { get; } = new();

        public Border VideoSurface { get; } = new() { Width = 320, Height = 180 };

        public FakeMediaSession Session { get; } = new();

        public PlaybackStatusTimer Timer { get; }

        public int OverlayRenderCount { get; private set; }

        public int OverlayClearCount { get; private set; }


        private readonly Window _window;

        public Harness(bool withSession = true)
        {
            var registry = new FakeMediaSessionRegistry();
            if (withSession)
            {
                registry.Current = Session;
            }

            Timer = new PlaybackStatusTimer(
                registry,
                Slider,
                Label,
                SubtitlesButton,
                VideoSurface,
                _ => OverlayRenderCount++,
                () => OverlayClearCount++);

            Timer.Start();

            var root = new StackPanel();
            root.Children.Add(Slider);
            root.Children.Add(VideoSurface);
            _window = new Window { Content = root, Width = 400, Height = 300 };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public void PumpTick() => Timer.Tick();

        public void PressSlider() => Mouse(w => w.MouseDown(SliderPoint, MouseButton.Left));

        public void ReleaseSlider() => Mouse(w => w.MouseUp(SliderPoint, MouseButton.Left));

        public void ResizeVideoSurface(double width, double height)
        {
            VideoSurface.Width = width;
            VideoSurface.Height = height;
            Dispatcher.UIThread.RunJobs();
        }

        private Point SliderPoint => new(Slider.Bounds.Width * 0.4, Slider.Bounds.Height / 2);

        private void Mouse(Action<Window> act)
        {
            act(_window);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
