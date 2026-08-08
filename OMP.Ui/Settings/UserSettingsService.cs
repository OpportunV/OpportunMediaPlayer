using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OMP.Ui.Settings;

internal sealed class UserSettingsService : IUserSettingsService
{
    public UserSettings Current { get; private set; } = new();

    private readonly ILogger<UserSettingsService> _logger;
    private readonly string _filePath;

    private const string FileName = "settings.json";
    private const string TempFileName = "settings.json.tmp";

    public UserSettingsService(ILogger<UserSettingsService> logger)
    {
        _logger = logger;

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppInfo.DirectoryName);

        _filePath = Path.Combine(directory, FileName);
        Load();
    }

    public void Save()
    {
        var tempPath = Path.Combine(Path.GetDirectoryName(_filePath)!, TempFileName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Current, UserSettingsJsonContext.Default.UserSettings));

            File.Move(tempPath, _filePath, overwrite: true);
            _logger.LogDebug("Saved settings to {FilePath}.", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {FilePath}.", _filePath);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("No settings file at {FilePath}; using defaults.", _filePath);
                return;
            }

            var loaded = JsonSerializer.Deserialize(
                File.ReadAllText(_filePath),
                UserSettingsJsonContext.Default.UserSettings);

            if (loaded is null)
            {
                _logger.LogWarning("Settings file {FilePath} was empty; using defaults.", _filePath);
                return;
            }

            Current = loaded;
            _logger.LogInformation("Loaded settings from {FilePath} (version {Version}).", _filePath, loaded.Version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read settings from {FilePath}; using defaults.", _filePath);
            Current = new UserSettings();
        }
    }
}
