using OMP.Lib.Audio;

namespace OMP.Ui.Models;

public static class AudioDelayInputRange
{
    public static decimal MinMs => (decimal)AudioDelayLimits.Min * 1000;

    public static decimal MaxMs => (decimal)AudioDelayLimits.Max * 1000;
}
