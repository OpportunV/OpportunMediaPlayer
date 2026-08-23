using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class SubtitleZoneGeometryTests
{
    [Fact]
    public void ClampPosition_WithinBounds_ReturnsUnchanged()
    {
        var position = SubtitleZoneGeometry.ClampPosition(50, 60, width: 100, height: 50, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(50, position.X);
        Assert.Equal(60, position.Y);
    }

    [Fact]
    public void ClampPosition_NegativeCoordinates_ClampToZero()
    {
        var position = SubtitleZoneGeometry.ClampPosition(-20, -10, width: 100, height: 50, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(0, position.X);
        Assert.Equal(0, position.Y);
    }

    [Fact]
    public void ClampPosition_BeyondCanvas_ClampsToLeaveRoomForSize()
    {
        var position = SubtitleZoneGeometry.ClampPosition(1000, 1000, width: 100, height: 50, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(380, position.X);
        Assert.Equal(220, position.Y);
    }

    [Fact]
    public void ClampSize_WithinBounds_ReturnsUnchanged()
    {
        var size = SubtitleZoneGeometry.ClampSize(
            100, 50, minSize: 24, left: 0, top: 0, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(100, size.Width);
        Assert.Equal(50, size.Height);
    }

    [Fact]
    public void ClampSize_BelowMinimum_ClampsToMinSize()
    {
        var size = SubtitleZoneGeometry.ClampSize(
            5, 5, minSize: 24, left: 0, top: 0, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(24, size.Width);
        Assert.Equal(24, size.Height);
    }

    [Fact]
    public void ClampSize_BeyondRemainingCanvas_ClampsToAvailableSpace()
    {
        var size = SubtitleZoneGeometry.ClampSize(
            1000, 1000, minSize: 24, left: 400, top: 220, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(80, size.Width);
        Assert.Equal(50, size.Height);
    }
}
