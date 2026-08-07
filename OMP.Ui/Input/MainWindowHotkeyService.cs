using Avalonia.Input;
using OMP.Ui.Controls;

namespace OMP.Ui.Input;

internal sealed class MainWindowHotkeyService : IMainWindowHotkeyService
{
    public bool Handle(Key key, IMainWindowCommands commands)
    {
        switch (key)
        {
            case Key.Space:
                commands.TogglePlayPause();
                return true;

            case Key.Left:
                commands.StepBack();
                return true;

            case Key.Right:
                commands.StepForward();
                return true;

            case Key.F:
                commands.ToggleFullscreen();
                return true;

            case Key.Escape:
                commands.ExitFullscreen();
                return true;

            case Key.OemPlus:
            case Key.Add:
                commands.IncreaseSpeed();
                return true;

            case Key.OemMinus:
            case Key.Subtract:
                commands.DecreaseSpeed();
                return true;
        }

        return false;
    }
}
