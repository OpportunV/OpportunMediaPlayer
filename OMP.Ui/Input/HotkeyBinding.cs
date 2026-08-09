using System;
using Avalonia.Input;
using OMP.Ui.Controls;

namespace OMP.Ui.Input;

internal sealed record HotkeyBinding(Key Key, KeyModifiers Modifiers, string Description, Action<IMainWindowCommands> Execute);
