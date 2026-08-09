using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;

namespace OMP.Ui.Input;

internal static class HotkeyReference
{
    public static readonly IReadOnlyList<HotkeyEntry> Entries = MainWindowHotkeys.All
        .Select(binding => new HotkeyEntry(FormatKeys(binding), binding.Description))
        .ToList();

    private static string FormatKeys(HotkeyBinding binding)
    {
        var keyLabel = FormatKeyLabel(binding.Key);
        return binding.Modifiers == KeyModifiers.None ? keyLabel : $"{binding.Modifiers} + {keyLabel}";
    }

    private static string FormatKeyLabel(Key key) => key switch
    {
        Key.Left => "Left Arrow",
        Key.Right => "Right Arrow",
        Key.Escape => "Esc",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        _ => key.ToString(),
    };
}
