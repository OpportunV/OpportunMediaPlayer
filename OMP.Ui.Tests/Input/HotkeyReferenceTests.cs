using Avalonia.Input;
using OMP.Ui.Input;

namespace OMP.Ui.Tests.Input;

public class HotkeyReferenceTests
{
    [Fact]
    public void Entries_HasOneEntryPerBinding() =>
        Assert.Equal(MainWindowHotkeys.All.Count, HotkeyReference.Entries.Count);

    [Fact]
    public void Entries_ModifierBinding_PrefixesModifiersOnKeyLabel()
    {
        var increaseSpeedBinding = MainWindowHotkeys.All.First(b => b.Key == Key.OemPeriod && b.Modifiers == KeyModifiers.Shift);
        var index = MainWindowHotkeys.All.ToList().IndexOf(increaseSpeedBinding);

        Assert.StartsWith("Shift", HotkeyReference.Entries[index].Keys);
    }

    [Fact]
    public void Entries_NoModifierBinding_HasNoModifierPrefix()
    {
        var playPauseBinding = MainWindowHotkeys.All.First(b => b.Key == Key.Space);
        var index = MainWindowHotkeys.All.ToList().IndexOf(playPauseBinding);

        Assert.DoesNotContain("+", HotkeyReference.Entries[index].Keys);
    }

    [Theory]
    [InlineData(Key.Left, "Left Arrow")]
    [InlineData(Key.Right, "Right Arrow")]
    [InlineData(Key.Escape, "Esc")]
    public void Entries_SpecialKeys_UseFriendlyLabel(Key key, string expectedLabel)
    {
        var binding = MainWindowHotkeys.All.First(b => b.Key == key);
        var index = MainWindowHotkeys.All.ToList().IndexOf(binding);

        Assert.Equal(expectedLabel, HotkeyReference.Entries[index].Keys);
    }
}
