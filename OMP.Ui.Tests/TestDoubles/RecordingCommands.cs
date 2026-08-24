using OMP.Ui.Services;

namespace OMP.Ui.Tests.TestDoubles;

internal sealed class RecordingCommands : IMainWindowCommands
{
    public List<string> Calls { get; } = [];

    public void Attach(MainWindowCommandContext context) => Calls.Add(nameof(Attach));

    public void TogglePlayPause() => Calls.Add(nameof(TogglePlayPause));

    public void StepBack() => Calls.Add(nameof(StepBack));

    public void StepForward() => Calls.Add(nameof(StepForward));

    public void IncreaseSpeed() => Calls.Add(nameof(IncreaseSpeed));

    public void DecreaseSpeed() => Calls.Add(nameof(DecreaseSpeed));

    public void SetSpeed(double speed) => Calls.Add(nameof(SetSpeed));

    public void ResetSpeed() => Calls.Add(nameof(ResetSpeed));

    public double? LastMasterVolume { get; private set; }

    public void SetMasterVolume(double volume)
    {
        LastMasterVolume = volume;
        Calls.Add(nameof(SetMasterVolume));
    }

    public void IncreaseMasterVolume() => Calls.Add(nameof(IncreaseMasterVolume));

    public void DecreaseMasterVolume() => Calls.Add(nameof(DecreaseMasterVolume));

    public void ToggleMute() => Calls.Add(nameof(ToggleMute));

    public void ToggleSubtitles() => Calls.Add(nameof(ToggleSubtitles));

    public void ToggleFullscreen() => Calls.Add(nameof(ToggleFullscreen));

    public void ExitFullscreen() => Calls.Add(nameof(ExitFullscreen));
}
