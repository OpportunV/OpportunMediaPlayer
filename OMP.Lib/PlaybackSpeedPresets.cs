namespace OMP.Lib;

public static class PlaybackSpeedPresets
{
    public static IReadOnlyList<double> Values { get; } = [0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0];

    private const double Epsilon = 1e-6;

    public static double Next(double current)
    {
        foreach (var preset in Values)
        {
            if (preset > current + Epsilon)
            {
                return preset;
            }
        }

        return Values[^1];
    }

    public static double Previous(double current)
    {
        for (var i = Values.Count - 1; i >= 0; i--)
        {
            if (Values[i] < current - Epsilon)
            {
                return Values[i];
            }
        }

        return Values[0];
    }

    public static bool IsPreset(double speed)
    {
        return Values.Any(preset => Math.Abs(preset - speed) < Epsilon);
    }
}
