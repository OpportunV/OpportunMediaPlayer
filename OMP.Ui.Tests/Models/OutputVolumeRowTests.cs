using System.ComponentModel;
using OMP.Lib.Audio.Output;
using OMP.Ui.Models;

namespace OMP.Ui.Tests.Models;

public class OutputVolumeRowTests
{
    [Fact]
    public void Constructor_SetsOutputLabelFromOutput()
    {
        var row = new OutputVolumeRow(new AudioOutput(1, "Speakers"), 100, false);

        Assert.Equal("Speakers", row.OutputLabel);
    }

    [Fact]
    public void Volume_SetToDifferentValue_RaisesPropertyChanged()
    {
        var row = new OutputVolumeRow(new AudioOutput(1, "Speakers"), 100, false);
        PropertyChangedEventArgs? raised = null;
        row.PropertyChanged += (_, e) => raised = e;

        row.Volume = 40;

        Assert.Equal(40, row.Volume);
        Assert.Equal(nameof(row.Volume), raised?.PropertyName);
    }

    [Fact]
    public void Volume_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var row = new OutputVolumeRow(new AudioOutput(1, "Speakers"), 100, false);
        var raised = false;
        row.PropertyChanged += (_, _) => raised = true;

        row.Volume = 100;

        Assert.False(raised);
    }
}
