namespace OMP.Lib.Audio;

internal static class AudioDelayProcessor
{
    public static double ComputeDelayedTargetSeconds(
        double targetMediaTimeSeconds, double userDelaySeconds, double outputLatencySeconds, double speed) =>
        targetMediaTimeSeconds - (userDelaySeconds - outputLatencySeconds) * speed;
}
