using OMP.Lib.Session;

namespace OMP.Lib.Tests;

public class PlaybackPositionTests
{
    [Theory]
    [InlineData(5, 10, 5)]
    [InlineData(10, 10, 10)]
    [InlineData(12.5, 10, 10)]
    public void ClampToDuration_NeverExceedsKnownDuration(double clock, double duration, double expected)
    {
        Assert.Equal(expected, PlaybackPosition.ClampToDuration(clock, duration));
    }

    [Fact]
    public void ClampToDuration_UnknownDuration_ReturnsClockUnchanged()
    {
        Assert.Equal(42, PlaybackPosition.ClampToDuration(42, 0));
    }

    [Theory]
    [InlineData(9.5, false)]
    [InlineData(10.5, false)]
    [InlineData(11, true)]
    [InlineData(30, true)]
    public void HasOverrunDuration_OnlyOncePastDurationPlusGrace(double clock, bool expected)
    {
        Assert.Equal(expected, PlaybackPosition.HasOverrunDuration(clock, durationSeconds: 10, graceSeconds: 1));
    }

    [Fact]
    public void HasOverrunDuration_UnknownDuration_NeverOverruns()
    {
        Assert.False(PlaybackPosition.HasOverrunDuration(1_000_000, durationSeconds: 0, graceSeconds: 1));
    }
}
