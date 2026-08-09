using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using OMP.Ui.Controls;

namespace OMP.Ui.Input;

internal sealed class MainWindowHotkeyService : IMainWindowHotkeyService
{
    private readonly Dictionary<(Key key, KeyModifiers modifiers), HotkeyBinding> _bindings;

    public MainWindowHotkeyService()
    {
        _bindings = MainWindowHotkeys.All.ToDictionary(k => (k.Key, k.Modifiers), k => k);
    }

    public bool Handle(Key key, KeyModifiers modifiers, IMainWindowCommands commands)
    {
        if (!_bindings.TryGetValue((key, modifiers), out var binding))
        {
            return false;
        }

        binding.Execute(commands);
        return true;

    }
}