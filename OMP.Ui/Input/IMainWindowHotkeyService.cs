using Avalonia.Input;
using OMP.Ui.Controls;

namespace OMP.Ui.Input;

public interface IMainWindowHotkeyService
{
    public bool Handle(Key key, KeyModifiers modifiers, IMainWindowCommands commands);
}
