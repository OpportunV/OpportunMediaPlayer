using System;

namespace OMP.Ui.Services;

public sealed class MainWindowCommandContext
{
    public required Func<bool> GetIsPlaying { get; init; }

    public required Func<bool> GetIsFullscreen { get; init; }

    public required Action<bool> SetIsPlaying { get; init; }

    public required Action<bool> SetIsMuted { get; init; }

    public required Action<double> SetSpeedDisplay { get; init; }

    public required Action<double> SetVolumeDisplay { get; init; }

    public required Action ToggleFullscreen { get; init; }

    public required Action ToggleSubtitles { get; init; }
}
