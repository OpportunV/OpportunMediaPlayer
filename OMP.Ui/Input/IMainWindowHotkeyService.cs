using Avalonia.Input;
using OMP.Ui.Services;

namespace OMP.Ui.Input;

public interface IMainWindowHotkeyService
{
    public bool Handle(Key key, KeyModifiers modifiers, IMainWindowCommands commands);
}
