namespace OMP.Lib.Audio;

internal static class PtsBaselineDetector
{
    public const double ThresholdSeconds = 1;

    public static double DetectOffset(double firstRawSeconds, double anchorSeconds) =>
        firstRawSeconds < ThresholdSeconds && anchorSeconds > ThresholdSeconds
            ? anchorSeconds - firstRawSeconds
            : 0;
}
