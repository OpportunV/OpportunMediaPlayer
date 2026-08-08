namespace OMP.Lib.Tests;

public class PlaybackSpeedPresetsTests
{
    [Fact]
    public void Next_FromAPreset_ReturnsTheNextOne()
    {
        Assert.Equal(1.25, PlaybackSpeedPresets.Next(1.0));
    }

    [Fact]
    public void Previous_FromAPreset_ReturnsThePreviousOne()
    {
        Assert.Equal(0.75, PlaybackSpeedPresets.Previous(1.0));
    }

    [Fact]
    public void Next_AtMax_Saturates()
    {
        Assert.Equal(PlaybackSpeedLimits.Max, PlaybackSpeedPresets.Next(PlaybackSpeedLimits.Max));
    }

    [Fact]
    public void Previous_AtMin_Saturates()
    {
        Assert.Equal(PlaybackSpeedLimits.Min, PlaybackSpeedPresets.Previous(PlaybackSpeedLimits.Min));
    }

    [Fact]
    public void Next_OffPreset_SnapsUpToTheNearestPreset()
    {
        Assert.Equal(1.25, PlaybackSpeedPresets.Next(1.1));
    }

    [Fact]
    public void Previous_OffPreset_SnapsDownToTheNearestPreset()
    {
        Assert.Equal(1.0, PlaybackSpeedPresets.Previous(1.1));
    }

    [Fact]
    public void Next_AtAnInteriorPreset_StepsOffItRatherThanReturningItself()
    {
        for (var i = 0; i < PlaybackSpeedPresets.Values.Count - 1; i++)
        {
            var preset = PlaybackSpeedPresets.Values[i];
            Assert.NotEqual(preset, PlaybackSpeedPresets.Next(preset));
        }
    }

    [Fact]
    public void Previous_AtAnInteriorPreset_StepsOffItRatherThanReturningItself()
    {
        for (var i = 1; i < PlaybackSpeedPresets.Values.Count; i++)
        {
            var preset = PlaybackSpeedPresets.Values[i];
            Assert.NotEqual(preset, PlaybackSpeedPresets.Previous(preset));
        }
    }

    [Fact]
    public void AllValues_AreWithinLimits()
    {
        Assert.All(
            PlaybackSpeedPresets.Values,
            value => Assert.InRange(value, PlaybackSpeedLimits.Min, PlaybackSpeedLimits.Max));
    }

    [Fact]
    public void IsPreset_RecognizesExactValuesOnly()
    {
        Assert.True(PlaybackSpeedPresets.IsPreset(1.0));
        Assert.True(PlaybackSpeedPresets.IsPreset(1.5));
        Assert.False(PlaybackSpeedPresets.IsPreset(1.1));
    }
}
