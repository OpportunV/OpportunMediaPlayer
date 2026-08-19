using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using OMP.Lib;
using OMP.Ui.Controls;

namespace OMP.Ui.Tests.Controls;

public class SpeedFlyoutViewTests
{
    private const double PresetEpsilon = 1e-9;

    [AvaloniaFact]
    public void Constructor_CreatesOneButtonPerPreset()
    {
        var view = new SpeedFlyoutView();

        Assert.Equal(PlaybackSpeedPresets.Values.Count, view.PresetsPanel.Children.Count);
    }

    [AvaloniaFact]
    public void SetSpeed_UpdatesSliderValueAndLabel()
    {
        var view = new SpeedFlyoutView();

        view.SetSpeed(1.5);

        Assert.Equal(1.5, view.SpeedSlider.Value);
        Assert.Equal("1.5x", view.SpeedValueLabel.Text);
    }

    [AvaloniaFact]
    public void PresetButtonClick_CommitsThatPresetSpeed()
    {
        var view = new SpeedFlyoutView();
        double? committed = null;
        view.SpeedCommitted += speed => committed = speed;
        var button = view.PresetsPanel.Children.OfType<Button>()
            .First(b => b.Tag is double tag && Math.Abs(tag - 1.5) < PresetEpsilon);

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1.5, committed);
    }

    [AvaloniaFact]
    public void Drag_UpdatesLabelWithoutCommitting_ThenCommitsOnRelease()
    {
        var (view, window) = CreateShown();
        var committedValues = new List<double>();
        view.SpeedCommitted += speed => committedValues.Add(speed);
        var point = CenterOf(view.SpeedSlider, window);

        window.MouseDown(point, MouseButton.Left);
        view.SpeedSlider.Value = 1.75;

        Assert.Empty(committedValues);
        Assert.Equal("1.75x", view.SpeedValueLabel.Text);

        window.MouseUp(point, MouseButton.Left);

        Assert.Equal([1.75], committedValues);
    }

    [AvaloniaFact]
    public void ValueChanged_WithoutPriorPointerPressed_DoesNotUpdateLabel()
    {
        var view = new SpeedFlyoutView();
        view.SetSpeed(1.0);

        view.SpeedSlider.Value = 1.75;

        Assert.Equal("1x", view.SpeedValueLabel.Text);
    }

    private static (SpeedFlyoutView View, Window Window) CreateShown()
    {
        var view = new SpeedFlyoutView();
        var window = new Window { Content = view };
        window.Show();
        return (view, window);
    }

    private static Point CenterOf(Visual control, Visual relativeTo)
    {
        var bounds = control.Bounds;
        return control.TranslatePoint(new Point(bounds.Width / 2, bounds.Height / 2), relativeTo)!.Value;
    }
}
