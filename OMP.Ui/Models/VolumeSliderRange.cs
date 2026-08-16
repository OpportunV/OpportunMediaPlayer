using OMP.Lib.Audio;

namespace OMP.Ui.Models;

public static class VolumeSliderRange
{
    public static double Min => AudioVolumeLimits.Min * 100;

    public static double Max => AudioVolumeLimits.Max * 100;
}
