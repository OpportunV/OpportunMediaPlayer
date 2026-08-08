using System.Text.Json.Serialization;

namespace OMP.Ui.Settings;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserSettings))]
internal sealed partial class UserSettingsJsonContext : JsonSerializerContext;
