using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Extensions;

public class OutputVolumeExtTests
{
    [Fact]
    public void MatchSettings_MatchingFriendlyName_PairsOutputWithSetting()
    {
        var outputs = new List<AudioOutput> { new(1, "Speakers"), new(2, "Headset") };
        var settings = new List<OutputVolumeSetting> { new() { FriendlyName = "Headset", Volume = 0.5 } };

        var matched = outputs.MatchSettings(settings).ToList();

        var pair = Assert.Single(matched);
        Assert.Equal("Headset", pair.Output.FriendlyName);
        Assert.Equal(0.5, pair.Setting.Volume);
    }

    [Fact]
    public void MatchSettings_NoMatchingOutput_IsExcluded()
    {
        var outputs = new List<AudioOutput> { new(1, "Speakers") };
        var settings = new List<OutputVolumeSetting> { new() { FriendlyName = "Unplugged Device" } };

        Assert.Empty(outputs.MatchSettings(settings));
    }

    [Fact]
    public void ToVolumeRows_KnownOutput_UsesStoredVolumeAndMuted()
    {
        var output = new AudioOutput(1, "Speakers");
        var routes = new List<AudioRoute> { new(new AudioStream(1, "aac", "Main", "en"), output) };
        var volumes = new Dictionary<int, OutputVolumeState> { [1] = new(0.5, true) };

        var row = Assert.Single(routes.ToVolumeRows(volumes));

        Assert.Equal(output, row.Output);
        Assert.Equal(50, row.VolumePercent);
        Assert.True(row.Muted);
    }

    [Fact]
    public void ToVolumeRows_UnknownOutput_DefaultsToFullVolumeUnmuted()
    {
        var output = new AudioOutput(1, "Speakers");
        var routes = new List<AudioRoute> { new(new AudioStream(1, "aac", "Main", "en"), output) };
        var volumes = new Dictionary<int, OutputVolumeState>();

        var row = Assert.Single(routes.ToVolumeRows(volumes));

        Assert.Equal(100, row.VolumePercent);
        Assert.False(row.Muted);
    }
}
