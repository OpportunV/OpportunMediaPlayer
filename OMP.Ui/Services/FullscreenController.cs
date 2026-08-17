using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace OMP.Ui.Services;

internal sealed class FullscreenController : IDisposable
{
    public bool IsFullscreen { get; private set; }

    private readonly Window _window;
    private readonly Control _topMenu;
    private readonly Control _overlayControls;
    private readonly Control _videoSurface;
    private readonly DispatcherTimer _overlayTimer = new();
    private WindowState _previousWindowState;
    private PixelPoint _previousPosition;
    private double _previousWidth;
    private double _previousHeight;

    public FullscreenController(Window window, Control topMenu, Control overlayControls, Control videoSurface)
    {
        _window = window;
        _topMenu = topMenu;
        _overlayControls = overlayControls;
        _videoSurface = videoSurface;
        _previousWindowState = window.WindowState;

        _overlayTimer.Interval = TimeSpan.FromSeconds(3);
        _overlayTimer.Tick += (_, _) =>
        {
            if (IsFullscreen)
            {
                _overlayControls.Opacity = 0;
                _overlayTimer.Stop();
            }
        };
    }

    public void Toggle()
    {
        IsFullscreen = !IsFullscreen;
        if (IsFullscreen)
        {
            _previousWindowState = _window.WindowState;

            if (_previousWindowState == WindowState.Normal)
            {
                _previousPosition = _window.Position;
                _previousWidth = _window.Width;
                _previousHeight = _window.Height;
            }

            _window.PointerMoved += OnPointerMoved;
            _window.PointerExited += OnPointerExited;
            _window.WindowState = WindowState.FullScreen;

            _topMenu.IsVisible = false;
            _overlayControls.Opacity = 1;
            UpdateVideoViewportMargin();

            _overlayTimer.Start();
        }
        else
        {
            _window.PointerMoved -= OnPointerMoved;
            _window.PointerExited -= OnPointerExited;
            _window.WindowState = _previousWindowState;

            if (_previousWindowState == WindowState.Normal)
            {
                _window.Position = _previousPosition;
                _window.Width = _previousWidth;
                _window.Height = _previousHeight;
            }

            _topMenu.IsVisible = true;
            _overlayControls.Opacity = 1;
            UpdateVideoViewportMargin();

            _overlayTimer.Stop();
        }
    }

    public void UpdateVideoViewportMargin()
    {
        var bottomMargin = IsFullscreen ? 0 : _overlayControls.Bounds.Height;
        _videoSurface.Margin = new Thickness(0, 0, 0, bottomMargin);
    }

    public void Dispose()
    {
        _overlayTimer.Stop();

        if (!IsFullscreen)
        {
            return;
        }

        _window.PointerMoved -= OnPointerMoved;
        _window.PointerExited -= OnPointerExited;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!IsFullscreen)
        {
            return;
        }

        _topMenu.IsVisible = false;
        _overlayControls.Opacity = 0;

        _overlayTimer.Stop();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsFullscreen)
        {
            return;
        }

        _topMenu.Opacity = 1;
        _overlayControls.Opacity = 1;

        _overlayTimer.Stop();
        _overlayTimer.Start();
    }
}
