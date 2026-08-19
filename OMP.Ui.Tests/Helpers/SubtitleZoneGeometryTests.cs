using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class SubtitleZoneGeometryTests
{
    [Fact]
    public void ClampPosition_WithinBounds_ReturnsUnchanged()
    {
        var (left, top) = SubtitleZoneGeometry.ClampPosition(50, 60, width: 100, height: 50, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(50, left);
        Assert.Equal(60, top);
    }

    [Fact]
    public void ClampPosition_NegativeCoordinates_ClampToZero()
    {
        var (left, top) = SubtitleZoneGeometry.ClampPosition(-20, -10, width: 100, height: 50, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(0, left);
        Assert.Equal(0, top);
    }

    [Fact]
    public void ClampPosition_BeyondCanvas_ClampsToLeaveRoomForSize()
    {
        var (left, top) = SubtitleZoneGeometry.ClampPosition(1000, 1000, width: 100, height: 50, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(380, left);
        Assert.Equal(220, top);
    }

    [Fact]
    public void ClampSize_WithinBounds_ReturnsUnchanged()
    {
        var (width, height) = SubtitleZoneGeometry.ClampSize(
            100, 50, minSize: 24, left: 0, top: 0, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(100, width);
        Assert.Equal(50, height);
    }

    [Fact]
    public void ClampSize_BelowMinimum_ClampsToMinSize()
    {
        var (width, height) = SubtitleZoneGeometry.ClampSize(
            5, 5, minSize: 24, left: 0, top: 0, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(24, width);
        Assert.Equal(24, height);
    }

    [Fact]
    public void ClampSize_BeyondRemainingCanvas_ClampsToAvailableSpace()
    {
        var (width, height) = SubtitleZoneGeometry.ClampSize(
            1000, 1000, minSize: 24, left: 400, top: 220, canvasWidth: 480, canvasHeight: 270);

        Assert.Equal(80, width);
        Assert.Equal(50, height);
    }
}
