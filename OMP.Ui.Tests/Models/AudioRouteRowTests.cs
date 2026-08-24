using System.ComponentModel;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Ui.Models;

namespace OMP.Ui.Tests.Models;

public class AudioRouteRowTests
{
    private static readonly AudioStream _mainStream = new(1, "aac", "Main", "en");
    private static readonly AudioStream _commentaryStream = new(2, "aac", "Commentary", "en");
    private static readonly AudioOutput _output = new(1, "Speakers");

    [Fact]
    public void Constructor_SetsOutputLabelFromRoute()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0);

        Assert.Equal("Speakers", row.OutputLabel);
    }

    [Fact]
    public void SelectedStreamOption_MatchingOptionAvailable_ReturnsIt()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0)
        {
            AvailableStreamOptions = [new AudioStreamOption(_mainStream), new AudioStreamOption(_commentaryStream)]
        };

        Assert.Equal(_mainStream.Id, row.SelectedStreamOption!.Stream.Id);
    }

    [Fact]
    public void SelectedStreamOption_NoMatchingOption_ReturnsNull()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0)
        {
            AvailableStreamOptions = [new AudioStreamOption(_commentaryStream)]
        };

        Assert.Null(row.SelectedStreamOption);
    }

    [Fact]
    public void SelectedStreamOption_SetToDifferentStream_RebuildsRouteAndRaisesPropertyChanged()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.SelectedStreamOption = new AudioStreamOption(_commentaryStream);

        Assert.Equal(_commentaryStream.Id, row.Route.Stream.Id);
        Assert.Equal([nameof(row.SelectedStreamOption)], raised);
    }

    [Fact]
    public void SelectedStreamOption_SetToSameStream_IsNoOp()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0);
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        row.SelectedStreamOption = new AudioStreamOption(_mainStream);

        Assert.Empty(raised);
    }

    [Fact]
    public void SelectedStreamOption_SetToNull_IsNoOp()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0);
        var raised = false;
        row.PropertyChanged += (_, _) => raised = true;

        row.SelectedStreamOption = null;

        Assert.False(raised);
        Assert.Equal(_mainStream.Id, row.Route.Stream.Id);
    }

    [Fact]
    public void Volume_SetToDifferentValue_RaisesPropertyChanged()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0);
        PropertyChangedEventArgs? raised = null;
        row.PropertyChanged += (_, e) => raised = e;

        row.Volume = 50;

        Assert.Equal(50, row.Volume);
        Assert.Equal(nameof(row.Volume), raised?.PropertyName);
    }

    [Fact]
    public void Volume_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0);
        var raised = false;
        row.PropertyChanged += (_, _) => raised = true;

        row.Volume = 100;

        Assert.False(raised);
    }

    [Fact]
    public void DelayMs_SetToDifferentValue_RaisesPropertyChanged()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0);
        PropertyChangedEventArgs? raised = null;
        row.PropertyChanged += (_, e) => raised = e;

        row.DelayMs = 25;

        Assert.Equal(25, row.DelayMs);
        Assert.Equal(nameof(row.DelayMs), raised?.PropertyName);
    }

    [Fact]
    public void CanDelete_SetToDifferentValue_RaisesPropertyChanged()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0);
        PropertyChangedEventArgs? raised = null;
        row.PropertyChanged += (_, e) => raised = e;

        row.CanDelete = true;

        Assert.True(row.CanDelete);
        Assert.Equal(nameof(row.CanDelete), raised?.PropertyName);
    }

    [Fact]
    public void CanDelete_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var row = new AudioRouteRow(new AudioRoute(_mainStream, _output), 100, false, 0) { CanDelete = true };
        var raised = false;
        row.PropertyChanged += (_, _) => raised = true;

        row.CanDelete = true;

        Assert.False(raised);
    }
}
