using System.Collections.Generic;

namespace OMP.Ui.Input;

internal sealed record HotkeyGroup(string Title, IReadOnlyList<HotkeyEntry> Entries);
