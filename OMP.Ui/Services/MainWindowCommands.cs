using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OMP.Lib;
using OMP.Lib.Session;

namespace OMP.Ui.Services;

internal sealed class MainWindowCommands(IMediaSessionRegistry mediaSessionRegistry, ILogger<MainWindowCommands> logger)
    : IMainWindowCommands
{
    private static readonly TimeSpan _seekStep = TimeSpan.FromSeconds(5);
    private MainWindowCommandContext? _context;
    private const double VolumeStep = 0.05;

    public void Attach(MainWindowCommandContext context)
    {
        _context = context;
    }

    public void TogglePlayPause() => _ = TogglePlayPauseAsync();

    public void StepBack() => _ = StepBackAsync();

    public void StepForward() => _ = StepForwardAsync();

    public void IncreaseSpeed() => _ = IncreaseSpeedAsync();

    public void DecreaseSpeed() => _ = DecreaseSpeedAsync();

    public void SetSpeed(double speed) => _ = ApplySpeedAsync(speed);

    public void ResetSpeed() => _ = ApplySpeedAsync(1.0);

    public void SetMasterVolume(double volume)
    {
        mediaSessionRegistry.Current?.SetMasterVolume(volume);
    }

    public void IncreaseMasterVolume()
    {
        AdjustMasterVolume(VolumeStep);
    }

    public void DecreaseMasterVolume()
    {
        AdjustMasterVolume(-VolumeStep);
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

    internal async Task TogglePlayPauseAsync()
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

        try
        {
            if (session.Duration > TimeSpan.Zero && session.CurrentTime >= session.Duration)
            {
                await Task.Run(() => session.Seek(TimeSpan.Zero));
            }

            session.Play();
            _context.SetIsPlaying(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "TogglePlayPause failed.");
        }
    }

    internal async Task StepBackAsync()
    {
        try
        {
            await Task.Run(() => mediaSessionRegistry.Current?.Step(-_seekStep));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "StepBack failed.");
        }
    }

    internal async Task StepForwardAsync()
    {
        try
        {
            await Task.Run(() => mediaSessionRegistry.Current?.Step(_seekStep));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "StepForward failed.");
        }
    }

    internal async Task IncreaseSpeedAsync()
    {
        var session = mediaSessionRegistry.Current;
        if (session != null)
        {
            await ApplySpeedAsync(PlaybackSpeedPresets.Next(session.Speed));
        }
    }

    internal async Task DecreaseSpeedAsync()
    {
        var session = mediaSessionRegistry.Current;
        if (session != null)
        {
            await ApplySpeedAsync(PlaybackSpeedPresets.Previous(session.Speed));
        }
    }

    internal async Task ApplySpeedAsync(double speed)
    {
        var session = mediaSessionRegistry.Current;
        if (session == null)
        {
            return;
        }

        try
        {
            await Task.Run(() => session.SetSpeed(speed));
            _context?.SetSpeedDisplay(session.Speed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SetSpeed failed.");
        }
    }

    private void AdjustMasterVolume(double delta)
    {
        var session = mediaSessionRegistry.Current;
        if (session == null)
        {
            return;
        }

        session.SetMasterVolume(session.MasterVolume + delta);
        _context?.SetVolumeDisplay(session.MasterVolume);
    }
}
