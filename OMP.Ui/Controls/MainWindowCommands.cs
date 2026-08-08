using System;
using OMP.Lib.Session;

namespace OMP.Ui.Controls;

internal sealed class MainWindowCommands(IMediaSessionRegistry mediaSessionRegistry) : IMainWindowCommands
{
    private static readonly TimeSpan _seekStep = TimeSpan.FromSeconds(5);
    private MainWindowCommandContext? _context;

    public void Attach(MainWindowCommandContext context)
    {
        _context = context;
    }

    public void TogglePlayPause()
    {
        var session = mediaSessionRegistry.Current;

        if (session == null || _context == null)
        {
            return;
        }

        if (_context.GetIsPlaying())
        {
            session.Pause();
            _context.SetIsPlaying(false);
            return;
        }

        session.Play();
        _context.SetIsPlaying(true);
    }

    public void StepBack()
    {
        mediaSessionRegistry.Current?.Step(-_seekStep);
    }

    public void StepForward()
    {
        mediaSessionRegistry.Current?.Step(_seekStep);
    }

    public void IncreaseSpeed()
    {
        ChangeSpeed(0.1);
    }

    public void DecreaseSpeed()
    {
        ChangeSpeed(-0.1);
    }

    public void SetMasterVolume(double volume)
    {
        mediaSessionRegistry.Current?.SetMasterVolume(volume);
    }

    public void ToggleMute()
    {
        var session = mediaSessionRegistry.Current;

        if (session == null)
        {
            return;
        }

        session.SetMasterMuted(!session.IsMuted);
        _context?.SetIsMuted(session.IsMuted);
    }

    public void ToggleFullscreen()
    {
        _context?.ToggleFullscreen();
    }

    public void ExitFullscreen()
    {
        if (_context?.GetIsFullscreen() == true)
        {
            _context.ToggleFullscreen();
        }
    }

    private void ChangeSpeed(double delta)
    {
        var session = mediaSessionRegistry.Current;
        session?.SetSpeed(session.Speed + delta);
    }
}
