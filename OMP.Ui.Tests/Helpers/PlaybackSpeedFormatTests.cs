using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class PlaybackSpeedFormatTests
{
    [Theory]
    [InlineData(1.0, "1x")]
    [InlineData(2.0, "2x")]
    [InlineData(0.5, "0.5x")]
    [InlineData(1.25, "1.25x")]
    public void Format_GivenSpeed_ReturnsExpectedLabel(double speed, string expected) =>
        Assert.Equal(expected, PlaybackSpeedFormat.Format(speed));
}
