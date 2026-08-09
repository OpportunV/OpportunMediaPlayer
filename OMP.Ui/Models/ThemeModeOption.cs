using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui.Models;

internal sealed class ThemeModeOption(ThemeMode mode)
{
    public ThemeMode Mode { get; } = mode;

    public string Label { get; } = mode.ToDisplayLabel();
}
