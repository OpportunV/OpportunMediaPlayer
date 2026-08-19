using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using OMP.Ui.Helpers;

namespace OMP.Ui.Services;

internal sealed class VideoRenderSurface(Image imageControl) : IDisposable
{
    public PixelSize? FrameSize { get; private set; }

    private WriteableBitmap? _bitmap;

    public void Render(int width, int height, byte[] pixelData, int length)
    {
        if (_bitmap == null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            imageControl.Source = _bitmap;
        }

        using var fb = _bitmap.Lock();

        unsafe
        {
            fixed (byte* src = pixelData)
            {
                Buffer.MemoryCopy(src, (void*)fb.Address, length, length);
            }
        }

        imageControl.InvalidateVisual();
        FrameSize = new PixelSize(width, height);
    }

    public Rect GetVideoContentRect(Size containerSize) =>
        FrameSize is { } frameSize ? VideoLetterbox.ComputeContentRect(frameSize, containerSize) : default;

    public void Reset()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        imageControl.Source = null;
        FrameSize = null;
    }

    public void Dispose() => Reset();
}
