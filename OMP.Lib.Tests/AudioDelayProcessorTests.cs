using OMP.Lib.Audio;

namespace OMP.Lib.Tests;

public class AudioDelayProcessorTests
{
    [Fact]
    public void ComputeDelayedTargetSeconds_ZeroDelayAndLatency_ReturnsTargetUnchanged()
    {
        var result = AudioDelayProcessor.ComputeDelayedTargetSeconds(
            targetMediaTimeSeconds: 10, userDelaySeconds: 0, outputLatencySeconds: 0, speed: 1);

        Assert.Equal(10, result);
    }

    [Fact]
    public void ComputeDelayedTargetSeconds_PositiveDelay_PushesReadinessLater()
    {
        var result = AudioDelayProcessor.ComputeDelayedTargetSeconds(
            targetMediaTimeSeconds: 10, userDelaySeconds: 0.5, outputLatencySeconds: 0, speed: 1);

        Assert.Equal(9.5, result, precision: 10);
    }

    [Fact]
    public void ComputeDelayedTargetSeconds_NegativeDelay_PushesReadinessEarlier()
    {
        var result = AudioDelayProcessor.ComputeDelayedTargetSeconds(
            targetMediaTimeSeconds: 10, userDelaySeconds: -0.5, outputLatencySeconds: 0, speed: 1);

        Assert.Equal(10.5, result, precision: 10);
    }

    [Fact]
    public void ComputeDelayedTargetSeconds_PositiveLatency_PushesReadinessEarlier()
    {
        var result = AudioDelayProcessor.ComputeDelayedTargetSeconds(
            targetMediaTimeSeconds: 10, userDelaySeconds: 0, outputLatencySeconds: 0.02, speed: 1);

        Assert.Equal(10.02, result, precision: 10);
    }

    [Fact]
    public void ComputeDelayedTargetSeconds_DelayAndLatencyCombined_ShiftAdditively()
    {
        var result = AudioDelayProcessor.ComputeDelayedTargetSeconds(
            targetMediaTimeSeconds: 10, userDelaySeconds: 0.5, outputLatencySeconds: 0.02, speed: 1);

        Assert.Equal(9.52, result, precision: 10);
    }

    [Theory]
    [InlineData(2.0)]
    [InlineData(0.5)]
    public void ComputeDelayedTargetSeconds_NonUnitSpeed_ScalesShiftBySpeed(double speed)
    {
        var result = AudioDelayProcessor.ComputeDelayedTargetSeconds(
            targetMediaTimeSeconds: 10, userDelaySeconds: 0.5, outputLatencySeconds: 0, speed: speed);

        Assert.Equal(10 - 0.5 * speed, result, precision: 10);
    }
}
