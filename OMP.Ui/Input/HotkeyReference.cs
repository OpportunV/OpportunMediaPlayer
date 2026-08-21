using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using OMP.Ui.Localization;

namespace OMP.Ui.Input;

internal static class HotkeyReference
{
    public static readonly IReadOnlyList<HotkeyEntry> Entries = MainWindowHotkeys.All
        .Select(binding => new HotkeyEntry(FormatKeys(binding), binding.Description))
        .ToList();

    public static readonly IReadOnlyList<HotkeyGroup> Groups = BuildGroups();

    private static IReadOnlyList<HotkeyGroup> BuildGroups()
    {
        HotkeyBinding Find(Key key, KeyModifiers modifiers = KeyModifiers.None) =>
            MainWindowHotkeys.All.First(b => b.Key == key && b.Modifiers == modifiers);

        HotkeyEntry Single(HotkeyBinding binding) => new(FormatKeys(binding), binding.Description);

        HotkeyEntry Merged(HotkeyBinding a, HotkeyBinding b, string description) =>
            new($"{FormatKeys(a)} / {FormatKeys(b)}", description);

        var playPause = Find(Key.Space);
        var stepBack = Find(Key.Left);
        var stepForward = Find(Key.Right);
        var decreaseSpeed = Find(Key.OemComma, KeyModifiers.Shift);
        var increaseSpeed = Find(Key.OemPeriod, KeyModifiers.Shift);
        var increaseVolume = Find(Key.Up);
        var decreaseVolume = Find(Key.Down);
        var toggleMute = Find(Key.M);
        var toggleFullscreen = Find(Key.F);
        var exitFullscreen = Find(Key.Escape);
        var toggleSubtitles = Find(Key.C);

        return
        [
            new HotkeyGroup(Strings.Hotkeys_GroupPlayback,
            [
                Single(playPause),
                Merged(stepBack, stepForward, Strings.Hotkeys_StepBackForward),
                Merged(decreaseSpeed, increaseSpeed, Strings.Hotkeys_SpeedDownUp)
            ]),
            new HotkeyGroup(Strings.Hotkeys_GroupAudio,
            [
                Merged(decreaseVolume, increaseVolume, Strings.Hotkeys_VolumeDownUp),
                Single(toggleMute)
            ]),
            new HotkeyGroup(Strings.Hotkeys_GroupView,
            [
                Single(toggleFullscreen),
                Single(exitFullscreen),
                Single(toggleSubtitles)
            ])
        ];
    }

    private static string FormatKeys(HotkeyBinding binding)
    {
        var keyLabel = FormatKeyLabel(binding.Key);
        return binding.Modifiers == KeyModifiers.None ? keyLabel : $"{binding.Modifiers} + {keyLabel}";
    }

    private static string FormatKeyLabel(Key key) => key switch
    {
        Key.Left => "Left Arrow",
        Key.Right => "Right Arrow",
        Key.Up => "Up Arrow",
        Key.Down => "Down Arrow",
        Key.Escape => "Esc",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        _ => key.ToString(),
    };
}
