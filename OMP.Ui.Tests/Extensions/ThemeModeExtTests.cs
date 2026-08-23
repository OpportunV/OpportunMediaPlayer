using Avalonia.Styling;
using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Extensions;

public class ThemeModeExtTests
{
    [Fact]
    public void ToThemeVariant_Light_MapsToThemeVariantLight() =>
        Assert.Equal(ThemeVariant.Light, ThemeMode.Light.ToThemeVariant());

    [Fact]
    public void ToThemeVariant_Dark_MapsToThemeVariantDark() =>
        Assert.Equal(ThemeVariant.Dark, ThemeMode.Dark.ToThemeVariant());

    [Fact]
    public void ToThemeVariant_System_MapsToThemeVariantDefault() =>
        Assert.Equal(ThemeVariant.Default, ThemeMode.System.ToThemeVariant());

    [Fact]
    public void ToDisplayLabel_LightAndDark_AreDistinct() =>
        Assert.NotEqual(ThemeMode.Light.ToDisplayLabel(), ThemeMode.Dark.ToDisplayLabel());
}
