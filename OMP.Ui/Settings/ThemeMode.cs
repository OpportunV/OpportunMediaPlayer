using System.Text.Json.Serialization;

namespace OMP.Ui.Settings;

[JsonConverter(typeof(JsonStringEnumConverter<ThemeMode>))]
public enum ThemeMode
{
    System,
    Light,
    Dark,
}
