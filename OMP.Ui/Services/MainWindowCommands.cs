using System;
using OMP.Lib;
using OMP.Lib.Session;

namespace OMP.Ui.Services;

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

        if (session.Duration > TimeSpan.Zero && session.CurrentTime >= session.Duration)
        {
            session.Seek(TimeSpan.Zero);
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
        var session = mediaSessionRegistry.Current;
        if (session != null)
        {
            ApplySpeed(PlaybackSpeedPresets.Next(session.Speed));
        }
    }

    public void DecreaseSpeed()
    {
        var session = mediaSessionRegistry.Current;
        if (session != null)
        {
            ApplySpeed(PlaybackSpeedPresets.Previous(session.Speed));
        }
    }

    public void SetSpeed(double speed)
    {
        ApplySpeed(speed);
    }

    public void ResetSpeed()
    {
        ApplySpeed(1.0);
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

    public void ToggleSubtitles()
    {
        _context?.ToggleSubtitles();
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

    private void ApplySpeed(double speed)
    {
        var session = mediaSessionRegistry.Current;
        if (session == null)
        {
            return;
        }

        session.SetSpeed(speed);
        _context?.SetSpeedDisplay(session.Speed);
    }
}
