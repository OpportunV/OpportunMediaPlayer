using Avalonia;

namespace OMP.Ui.Helpers;

internal static class VideoLetterbox
{
    public static Rect ComputeContentRect(PixelSize frameSize, Size containerSize)
    {
        if (containerSize.Width <= 0 || containerSize.Height <= 0)
        {
            return default;
        }

        var videoRatio = (double)frameSize.Width / frameSize.Height;
        var containerRatio = containerSize.Width / containerSize.Height;

        double width, height;
        if (videoRatio > containerRatio)
        {
            width = containerSize.Width;
            height = containerSize.Width / videoRatio;
        }
        else
        {
            height = containerSize.Height;
            width = containerSize.Height * videoRatio;
        }

        var x = (containerSize.Width - width) / 2;
        var y = (containerSize.Height - height) / 2;
        return new Rect(x, y, width, height);
    }
}
