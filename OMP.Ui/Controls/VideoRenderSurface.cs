using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using OMP.Lib.Video;

namespace OMP.Ui.Controls;

public sealed class VideoRenderSurface(Image imageControl) : IDisposable
{
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
    }

    public void Reset()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        imageControl.Source = null;
    }

    public void Dispose() => Reset();
}
