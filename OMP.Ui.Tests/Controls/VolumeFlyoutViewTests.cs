using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OMP.Lib.Audio.Output;
using OMP.Ui.Controls;

namespace OMP.Ui.Tests.Controls;

public class VolumeFlyoutViewTests
{
    [AvaloniaFact]
    public void SetOutputs_PopulatesOneRowPerOutput()
    {
        var (view, _) = CreateShown();

        view.SetOutputs([(new AudioOutput(1, "Speakers"), 80.0, false), (new AudioOutput(2, "Headset"), 50.0, true)]);
        Dispatcher.UIThread.RunJobs();

        var sliders = view.OutputsPanel.GetVisualDescendants().OfType<Slider>().Where(s => s.Name == "RowVolumeSlider").ToList();
        Assert.Equal(2, sliders.Count);
    }

    [AvaloniaFact]
    public void RowVolumeChanged_RaisesOutputVolumeChangedWithRowsOutput()
    {
        var (view, _) = CreateShown();
        var output = new AudioOutput(1, "Speakers");
        view.SetOutputs([(output, 80.0, false)]);
        Dispatcher.UIThread.RunJobs();

        (AudioOutput Output, double Volume)? raised = null;
        view.OutputVolumeChanged += (o, v) => raised = (o, v);
        var slider = view.OutputsPanel.GetVisualDescendants().OfType<Slider>().First(s => s.Name == "RowVolumeSlider");

        slider.Value = 40;

        Assert.NotNull(raised);
        Assert.Equal(output, raised!.Value.Output);
        Assert.Equal(40, raised.Value.Volume);
    }

    [AvaloniaFact]
    public void RowVolumeReleased_RaisesOutputVolumeCommittedForThatRow()
    {
        var (view, _) = CreateShown();
        var output = new AudioOutput(1, "Speakers");
        view.SetOutputs([(output, 80.0, false)]);
        Dispatcher.UIThread.RunJobs();

        AudioOutput? committed = null;
        view.OutputVolumeCommitted += o => committed = o;
        var slider = view.OutputsPanel.GetVisualDescendants().OfType<Slider>().First(s => s.Name == "RowVolumeSlider");
        var pointer = new Pointer(0, PointerType.Mouse, isPrimary: true);

        pointer.Capture(slider);
        pointer.Capture(null);

        Assert.Equal(output, committed);
    }

    [AvaloniaFact]
    public void RowMuteChanged_RaisesOutputMuteChangedWithNewState()
    {
        var (view, _) = CreateShown();
        var output = new AudioOutput(1, "Speakers");
        view.SetOutputs([(output, 80.0, false)]);
        Dispatcher.UIThread.RunJobs();

        (AudioOutput Output, bool Muted)? raised = null;
        view.OutputMuteChanged += (o, m) => raised = (o, m);
        var toggle = view.OutputsPanel.GetVisualDescendants().OfType<ToggleButton>().First(t => t.Name == "RowMuteButton");

        toggle.IsChecked = true;

        Assert.NotNull(raised);
        Assert.Equal(output, raised!.Value.Output);
        Assert.True(raised.Value.Muted);
    }

    private static (VolumeFlyoutView View, Window Window) CreateShown()
    {
        var view = new VolumeFlyoutView();
        var window = new Window { Content = view };
        window.Show();
        return (view, window);
    }
}
