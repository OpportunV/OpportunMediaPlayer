using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using OMP.Ui.Settings;

namespace OMP.Ui.Services;

/// <summary>
/// Remembers the window's size, position and maximized state across runs.
/// <para>
/// Two phases on purpose. <see cref="Restore"/> has to run before the fullscreen controller is
/// constructed, because that controller captures the current <see cref="WindowState"/> as the
/// state to return to when leaving fullscreen - restoring a maximized window afterward would
/// make it drop to Normal on the first fullscreen round trip. <see cref="StartPersisting"/> then
/// runs once the controller exists, since every handler consults it.
/// </para>
/// </summary>
internal sealed class WindowGeometryPersistence(Window window, IUserSettingsService settings, Func<bool> isFullscreen)
{
    public void Restore()
    {
        var saved = settings.Current.Window;

        if (saved is { Width: { } width, Height: { } height })
        {
            window.Width = width;
            window.Height = height;
        }

        if (saved is { Left: { } left, Top: { } top })
        {
            var position = new PixelPoint((int)left, (int)top);

            if (IsPositionOnAnyScreen(position))
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = position;
            }
        }

        if (saved.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    public void StartPersisting()
    {
        window.PositionChanged += (_, _) =>
        {
            if (window.WindowState != WindowState.Normal || isFullscreen())
            {
                return;
            }

            settings.Current.Window.Left = window.Position.X;
            settings.Current.Window.Top = window.Position.Y;
        };

        window.Resized += (_, _) =>
        {
            if (window.WindowState != WindowState.Normal || isFullscreen())
            {
                return;
            }

            settings.Current.Window.Width = window.Width;
            settings.Current.Window.Height = window.Height;
        };

        window.PropertyChanged += (_, e) =>
        {
            if (isFullscreen() || e.Property != Window.WindowStateProperty)
            {
                return;
            }

            if (window.WindowState is WindowState.Normal or WindowState.Maximized)
            {
                settings.Current.Window.IsMaximized = window.WindowState == WindowState.Maximized;
            }
        };
    }

    private bool IsPositionOnAnyScreen(PixelPoint position)
    {
        try
        {
            return window.Screens.All.Count == 0 || window.Screens.All.Any(screen => screen.Bounds.Contains(position));
        }
        catch (Exception)
        {
            return true;
        }
    }
}
