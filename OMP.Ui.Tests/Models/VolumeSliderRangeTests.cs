using OMP.Ui.Models;

namespace OMP.Ui.Tests.Models;

// Hardcoded rather than re-derived from AudioVolumeLimits.Min/Max * 100 - see the comment on
// AudioDelayInputRangeTests for why: this is a deliberate trip wire on the user-facing slider
// range, not a re-derivation of the property's own formula.
public class VolumeSliderRangeTests
{
    [Fact]
    public void Min_IsZeroPercent() => Assert.Equal(0, VolumeSliderRange.Min);

    [Fact]
    public void Max_IsTwoHundredPercent() => Assert.Equal(200, VolumeSliderRange.Max);
}
