using System.Collections.Generic;
using Avalonia.Input;
using OMP.Ui.Localization;

namespace OMP.Ui.Input;

internal static class MainWindowHotkeys
{
    public static readonly IReadOnlyList<HotkeyBinding> All =
    [
        new(Key.Space, KeyModifiers.None, Strings.Hotkeys_PlayPause, c => c.TogglePlayPause()),
        new(Key.Left, KeyModifiers.None, Strings.Hotkeys_StepBack, c => c.StepBack()),
        new(Key.Right, KeyModifiers.None, Strings.Hotkeys_StepForward, c => c.StepForward()),
        new(Key.F, KeyModifiers.None, Strings.Hotkeys_ToggleFullscreen, c => c.ToggleFullscreen()),
        new(Key.C, KeyModifiers.None, Strings.Hotkeys_ToggleSubtitles, c => c.ToggleSubtitles()),
        new(Key.M, KeyModifiers.None, Strings.Hotkeys_ToggleMute, c => c.ToggleMute()),
        new(Key.Escape, KeyModifiers.None, Strings.Hotkeys_ExitFullscreen, c => c.ExitFullscreen()),
        new(Key.OemComma, KeyModifiers.Shift, Strings.Hotkeys_DecreaseSpeed, c => c.DecreaseSpeed()),
        new(Key.OemPeriod, KeyModifiers.Shift, Strings.Hotkeys_IncreaseSpeed, c => c.IncreaseSpeed()),
        new(Key.Up, KeyModifiers.None, Strings.Hotkeys_IncreaseVolume, c => c.IncreaseMasterVolume()),
        new(Key.Down, KeyModifiers.None, Strings.Hotkeys_DecreaseVolume, c => c.DecreaseMasterVolume()),
    ];
}
