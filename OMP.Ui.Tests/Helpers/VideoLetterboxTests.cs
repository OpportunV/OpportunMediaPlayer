using Avalonia;
using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class VideoLetterboxTests
{
    [Fact]
    public void ComputeContentRect_WiderVideoThanContainer_LetterboxesTopAndBottom()
    {
        var rect = VideoLetterbox.ComputeContentRect(new PixelSize(1920, 1080), new Size(1000, 1000));

        Assert.Equal(1000, rect.Width);
        Assert.Equal(1000.0 * 1080 / 1920, rect.Height, 3);
        Assert.Equal(0, rect.X);
        Assert.True(rect.Y > 0);
    }

    [Fact]
    public void ComputeContentRect_TallerVideoThanContainer_LetterboxesLeftAndRight()
    {
        var rect = VideoLetterbox.ComputeContentRect(new PixelSize(1080, 1920), new Size(1000, 1000));

        Assert.Equal(1000, rect.Height);
        Assert.Equal(1000.0 * 1080 / 1920, rect.Width, 3);
        Assert.Equal(0, rect.Y);
        Assert.True(rect.X > 0);
    }

    [Fact]
    public void ComputeContentRect_MatchingAspectRatio_FillsContainerExactly()
    {
        var rect = VideoLetterbox.ComputeContentRect(new PixelSize(1920, 1080), new Size(1920, 1080));

        Assert.Equal(new Rect(0, 0, 1920, 1080), rect);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    public void ComputeContentRect_NonPositiveContainerDimension_ReturnsDefault(double width, double height) =>
        Assert.Equal(default, VideoLetterbox.ComputeContentRect(new PixelSize(1920, 1080), new Size(width, height)));
}
