using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Moq;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Services;

public class WindowGeometryPersistenceTests
{
    [AvaloniaFact]
    public void Restore_AppliesSavedSize()
    {
        var h = new Harness(w => { w.Width = 1024; w.Height = 768; });

        h.Geometry.Restore();

        Assert.Equal(1024, h.Window.Width);
        Assert.Equal(768, h.Window.Height);
    }

    [AvaloniaFact]
    public void Restore_MaximizedIsRestoredBeforeAnyoneCapturesTheWindowState()
    {
        var h = new Harness(w => w.IsMaximized = true);

        h.Geometry.Restore();

        Assert.Equal(WindowState.Maximized, h.Window.WindowState);
    }

    [AvaloniaFact]
    public void Restore_NeverConsultsFullscreen_SoItIsSafeBeforeTheControllerExists()
    {
        var h = new Harness(w => { w.Width = 800; w.Height = 600; w.IsMaximized = true; });

        h.Geometry.Restore();

        Assert.Equal(0, h.IsFullscreenCallCount);
    }

    [AvaloniaFact]
    public void StartPersisting_WhileFullscreen_DoesNotOverwriteSavedGeometry()
    {
        var h = new Harness(w => { w.Width = 1024; w.Height = 768; });
        h.IsFullscreen = true;
        h.Geometry.StartPersisting();

        h.Window.Width = 300;
        h.Window.Height = 200;

        Assert.Equal(1024, h.Settings.Window.Width);
        Assert.Equal(768, h.Settings.Window.Height);
    }

    private sealed class Harness
    {
        public Window Window { get; } = new();

        public UserSettings Settings { get; } = new();

        public WindowGeometryPersistence Geometry { get; }

        public bool IsFullscreen { get; set; }

        public int IsFullscreenCallCount { get; private set; }

        public Harness(Action<WindowSettings>? configureSaved = null)
        {
            configureSaved?.Invoke(Settings.Window);

            var settingsService = new Mock<IUserSettingsService>();
            settingsService.Setup(s => s.Current).Returns(Settings);

            Geometry = new WindowGeometryPersistence(
                Window,
                settingsService.Object,
                () =>
                {
                    IsFullscreenCallCount++;
                    return IsFullscreen;
                });
        }
    }
}
