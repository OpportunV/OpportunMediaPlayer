using Avalonia.Input;
using OMP.Ui.Input;
using OMP.Ui.Tests.TestDoubles;

namespace OMP.Ui.Tests.Input;

public class MainWindowHotkeyServiceTests
{
    [Fact]
    public void Handle_KnownBinding_InvokesMatchingCommandAndReturnsTrue()
    {
        var service = new MainWindowHotkeyService();
        var commands = new RecordingCommands();

        var handled = service.Handle(Key.Space, KeyModifiers.None, commands);

        Assert.True(handled);
        Assert.Equal([nameof(commands.TogglePlayPause)], commands.Calls);
    }

    [Fact]
    public void Handle_UnknownKey_ReturnsFalseAndInvokesNothing()
    {
        var service = new MainWindowHotkeyService();
        var commands = new RecordingCommands();

        var handled = service.Handle(Key.Z, KeyModifiers.None, commands);

        Assert.False(handled);
        Assert.Empty(commands.Calls);
    }

    [Fact]
    public void Handle_SameKeyDifferentModifiers_DispatchesDifferentBinding()
    {
        var service = new MainWindowHotkeyService();
        var commands = new RecordingCommands();

        service.Handle(Key.OemPeriod, KeyModifiers.Shift, commands);

        Assert.Equal([nameof(commands.IncreaseSpeed)], commands.Calls);
    }

    [Fact]
    public void Handle_KnownKeyWrongModifiers_ReturnsFalse()
    {
        var service = new MainWindowHotkeyService();
        var commands = new RecordingCommands();

        var handled = service.Handle(Key.Space, KeyModifiers.Control, commands);

        Assert.False(handled);
    }
}
