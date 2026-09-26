using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using OMP.Ui.Services;

namespace OMP.Ui.Tests.Services;

public class FullscreenControllerTests
{
    [AvaloniaFact]
    public void Toggle_FromNormal_EntersFullscreenAndHidesTopMenu()
    {
        var (controller, window, topMenu, _, _) = CreateController();

        controller.Toggle();

        Assert.True(controller.IsFullscreen);
        Assert.Equal(WindowState.FullScreen, window.WindowState);
        Assert.False(topMenu.IsVisible);
    }

    [AvaloniaFact]
    public void Toggle_Twice_RestoresNormalStateGeometryAndTopMenu()
    {
        var (controller, window, topMenu, _, _) = CreateController();
        window.WindowState = WindowState.Normal;
        window.Position = new(10, 20);
        window.Width = 800;
        window.Height = 600;

        controller.Toggle();
        controller.Toggle();

        Assert.False(controller.IsFullscreen);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.True(topMenu.IsVisible);
        Assert.Equal(800, window.Width);
        Assert.Equal(600, window.Height);
    }

    [AvaloniaFact]
    public void UpdateVideoViewportMargin_WhenNotFullscreen_MatchesOverlayHeight()
    {
        var (controller, _, _, overlayControls, videoSurface) = CreateController();
        overlayControls.Height = 48;
        Dispatcher.UIThread.RunJobs();

        controller.UpdateVideoViewportMargin();

        Assert.Equal(new(0, 0, 0, 48), videoSurface.Margin);
    }

    [AvaloniaFact]
    public void UpdateVideoViewportMargin_WhenFullscreen_UsesZeroMargin()
    {
        var (controller, _, _, _, videoSurface) = CreateController();

        controller.Toggle();

        Assert.Equal(new(0), videoSurface.Margin);
    }

    [AvaloniaFact]
    public void Dispose_WhileFullscreen_DoesNotThrow()
    {
        var (controller, _, _, _, _) = CreateController();
        controller.Toggle();

        var exception = Record.Exception(() => controller.Dispose());

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void PointerExited_WithPositionStillInsideWindow_KeepsOverlayVisible()
    {
        var (controller, window, _, overlayControls, _) = CreateController();
        controller.Toggle();

        RaisePointerExited(window, new(window.ClientSize.Width / 2, window.ClientSize.Height / 2));

        Assert.Equal(1, overlayControls.Opacity);
    }

    [AvaloniaFact]
    public void PointerExited_WithPositionOutsideWindow_HidesOverlay()
    {
        var (controller, window, _, overlayControls, _) = CreateController();
        controller.Toggle();

        RaisePointerExited(window, new(-10, -10));

        Assert.Equal(0, overlayControls.Opacity);
    }

    private static void RaisePointerExited(Window window, Point position)
    {
        var pointer = new Pointer(0, PointerType.Mouse, isPrimary: true);
#pragma warning disable CS0618 // no headless equivalent can produce an in-bounds exit;
        var args = new PointerEventArgs(
            InputElement.PointerExitedEvent,
            window,
            pointer,
            window,
            position,
            0,
            new(),
            KeyModifiers.None);
#pragma warning restore CS0618
        window.RaiseEvent(args);
    }

    private static (FullscreenController Controller, Window Window, Control TopMenu, Control OverlayControls, Control
        VideoSurface) CreateController()
    {
        var window = new Window { Width = 400, Height = 300 };
        var topMenu = new Border();
        var overlayControls = new Border();
        var videoSurface = new Border();
        window.Content = new Panel { Children = { topMenu, overlayControls, videoSurface } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var controller = new FullscreenController(window, topMenu, overlayControls, videoSurface);
        return (controller, window, topMenu, overlayControls, videoSurface);
    }
}