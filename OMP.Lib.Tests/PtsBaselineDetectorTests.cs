using OMP.Lib.Audio;

namespace OMP.Lib.Tests;

public class PtsBaselineDetectorTests
{
    [Fact]
    public void DetectOffset_FirstPacketNearZero_AnchorFarFromZero_ReturnsOffsetOntoAnchor()
    {
        var result = PtsBaselineDetector.DetectOffset(firstRawSeconds: 0.024, anchorSeconds: 24);

        Assert.Equal(23.976, result, precision: 10);
    }

    [Fact]
    public void DetectOffset_FirstPacketNearAnchor_ReturnsZero()
    {
        var result = PtsBaselineDetector.DetectOffset(firstRawSeconds: 23.995, anchorSeconds: 24);

        Assert.Equal(0, result);
    }

    [Fact]
    public void DetectOffset_AnchorAlsoNearZero_ReturnsZero()
    {
        var result = PtsBaselineDetector.DetectOffset(firstRawSeconds: 0.024, anchorSeconds: 0.5);

        Assert.Equal(0, result);
    }

    [Fact]
    public void DetectOffset_FirstPacketFarFromZero_ReturnsZeroRegardlessOfAnchor()
    {
        var result = PtsBaselineDetector.DetectOffset(firstRawSeconds: 5, anchorSeconds: 24);

        Assert.Equal(0, result);
    }

    [Fact]
    public void DetectOffset_BothAtZero_ReturnsZero()
    {
        var result = PtsBaselineDetector.DetectOffset(firstRawSeconds: 0, anchorSeconds: 0);

        Assert.Equal(0, result);
    }
}
