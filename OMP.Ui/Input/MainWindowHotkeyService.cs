using Avalonia.Input;
using OMP.Ui.Controls;

namespace OMP.Ui.Input;

internal sealed class MainWindowHotkeyService : IMainWindowHotkeyService
{
    public bool Handle(Key key, KeyModifiers modifiers, IMainWindowCommands commands)
    {
        return modifiers switch
        {
            KeyModifiers.None => HandleUnmodified(key, commands),
            KeyModifiers.Shift => HandleShift(key, commands),
            _ => false
        };
    }

    private static bool HandleUnmodified(Key key, IMainWindowCommands commands)
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

            case Key.C:
                commands.ToggleSubtitles();
                return true;

            case Key.Escape:
                commands.ExitFullscreen();
                return true;
        }

        return false;
    }

    private static bool HandleShift(Key key, IMainWindowCommands commands)
    {
        switch (key)
        {
            case Key.OemComma:
                commands.DecreaseSpeed();
                return true;

            case Key.OemPeriod:
                commands.IncreaseSpeed();
                return true;
        }

        return false;
    }
}
