using Avalonia.Styling;
using OMP.Ui.Localization;
using OMP.Ui.Settings;

namespace OMP.Ui.Extensions;

internal static class ThemeModeExt
{
    extension(ThemeMode mode)
    {
        public ThemeVariant ToThemeVariant() => mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        public string ToDisplayLabel() => mode switch
        {
            ThemeMode.Light => Strings.ThemeMode_Light,
            ThemeMode.Dark => Strings.ThemeMode_Dark,
            _ => Strings.Common_SystemDefault,
        };
    }
}
