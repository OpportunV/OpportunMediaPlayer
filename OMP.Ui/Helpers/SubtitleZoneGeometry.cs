using System;

namespace OMP.Ui.Helpers;

internal static class SubtitleZoneGeometry
{
    public static (double Left, double Top) ClampPosition(
        double left, double top, double width, double height, double canvasWidth, double canvasHeight) => (
        Math.Clamp(left, 0, canvasWidth - width),
        Math.Clamp(top, 0, canvasHeight - height));

    public static (double Width, double Height) ClampSize(
        double width, double height, double minSize, double left, double top, double canvasWidth, double canvasHeight) => (
        Math.Clamp(width, minSize, canvasWidth - left),
        Math.Clamp(height, minSize, canvasHeight - top));
}
