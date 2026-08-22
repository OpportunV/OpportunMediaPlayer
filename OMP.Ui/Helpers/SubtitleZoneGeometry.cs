using System;
using Avalonia;

namespace OMP.Ui.Helpers;

internal static class SubtitleZoneGeometry
{
    public static Point ClampPosition(
        double left, double top, double width, double height, double canvasWidth, double canvasHeight) => new(
        Math.Clamp(left, 0, canvasWidth - width),
        Math.Clamp(top, 0, canvasHeight - height));

    public static Size ClampSize(
        double width, double height, double minSize, double left, double top, double canvasWidth, double canvasHeight) => new(
        Math.Clamp(width, minSize, canvasWidth - left),
        Math.Clamp(height, minSize, canvasHeight - top));
}
