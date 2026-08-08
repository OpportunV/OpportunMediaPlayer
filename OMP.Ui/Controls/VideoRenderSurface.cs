using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using OMP.Lib.Video;

namespace OMP.Ui.Controls;

internal sealed class VideoRenderSurface(Image imageControl) : IDisposable
{
    public PixelSize? FrameSize { get; private set; }

    private WriteableBitmap? _bitmap;

    public void Render(VideoFrame frame)
    {
        if (_bitmap == null ||
            _bitmap.PixelSize.Width != frame.Width ||
            _bitmap.PixelSize.Height != frame.Height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(frame.Width, frame.Height),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            imageControl.Source = _bitmap;
        }

        using var fb = _bitmap.Lock();

        unsafe
        {
            Buffer.MemoryCopy((void*)frame.DataPtr, (void*)fb.Address, frame.DataLength, frame.DataLength);
        }

        imageControl.InvalidateVisual();
        FrameSize = new PixelSize(frame.Width, frame.Height);
    }

    public Rect GetVideoContentRect(Size containerSize)
    {
        if (FrameSize is not { } frameSize || containerSize.Width <= 0 || containerSize.Height <= 0)
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

    public void Reset()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        imageControl.Source = null;
        FrameSize = null;
    }

    public void Dispose() => Reset();
}
