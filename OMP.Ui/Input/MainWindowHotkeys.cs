using System.Collections.Generic;
using Avalonia.Input;

namespace OMP.Ui.Input;

internal static class MainWindowHotkeys
{
    public static readonly IReadOnlyList<HotkeyBinding> All =
    [
        new(Key.Space, KeyModifiers.None, "Play / pause", c => c.TogglePlayPause()),
        new(Key.Left, KeyModifiers.None, "Step back", c => c.StepBack()),
        new(Key.Right, KeyModifiers.None, "Step forward", c => c.StepForward()),
        new(Key.F, KeyModifiers.None, "Toggle fullscreen", c => c.ToggleFullscreen()),
        new(Key.C, KeyModifiers.None, "Toggle subtitles", c => c.ToggleSubtitles()),
        new(Key.Escape, KeyModifiers.None, "Exit fullscreen", c => c.ExitFullscreen()),
        new(Key.OemComma, KeyModifiers.Shift, "Decrease playback speed", c => c.DecreaseSpeed()),
        new(Key.OemPeriod, KeyModifiers.Shift, "Increase playback speed", c => c.IncreaseSpeed()),
    ];
}
