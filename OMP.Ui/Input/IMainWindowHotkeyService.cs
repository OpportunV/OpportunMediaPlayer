using Avalonia.Input;
using OMP.Ui.Controls;

namespace OMP.Ui.Input;

internal interface IMainWindowHotkeyService
{
    public bool Handle(Key key, IMainWindowCommands commands);
}
