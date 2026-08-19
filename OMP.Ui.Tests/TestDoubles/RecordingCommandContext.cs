using OMP.Ui.Services;

namespace OMP.Ui.Tests.TestDoubles;

internal sealed class RecordingCommandContext
{
    public bool IsPlaying { get; set; }

    public bool IsFullscreen { get; set; }

    public bool? LastSetIsMuted { get; private set; }

    public double? LastSpeedDisplay { get; private set; }

    public double? LastVolumeDisplay { get; private set; }

    public int ToggleFullscreenCallCount { get; private set; }

    public int ToggleSubtitlesCallCount { get; private set; }

    public MainWindowCommandContext ToContext() => new()
    {
        GetIsPlaying = () => IsPlaying,
        GetIsFullscreen = () => IsFullscreen,
        SetIsPlaying = value => IsPlaying = value,
        SetIsMuted = value => LastSetIsMuted = value,
        SetSpeedDisplay = value => LastSpeedDisplay = value,
        SetVolumeDisplay = value => LastVolumeDisplay = value,
        ToggleFullscreen = () =>
        {
            ToggleFullscreenCallCount++;
            IsFullscreen = !IsFullscreen;
        },
        ToggleSubtitles = () => ToggleSubtitlesCallCount++
    };
}
