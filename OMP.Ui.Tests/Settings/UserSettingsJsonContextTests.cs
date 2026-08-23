using System.Text.Json;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Settings;

public class UserSettingsJsonContextTests
{
    [Fact]
    public void RoundTrip_PreservesScalarValues()
    {
        var original = new UserSettings
        {
            MasterVolume = 0.75,
            IsMuted = true,
            PlaybackSpeed = 1.5,
            Theme = ThemeMode.Dark,
            Language = "ru"
        };

        var json = JsonSerializer.Serialize(original, UserSettingsJsonContext.Default.UserSettings);
        var roundTripped = JsonSerializer.Deserialize(json, UserSettingsJsonContext.Default.UserSettings);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.MasterVolume, roundTripped.MasterVolume);
        Assert.Equal(original.IsMuted, roundTripped.IsMuted);
        Assert.Equal(original.PlaybackSpeed, roundTripped.PlaybackSpeed);
        Assert.Equal(original.Theme, roundTripped.Theme);
        Assert.Equal(original.Language, roundTripped.Language);
    }

    [Fact]
    public void RoundTrip_PreservesOutputVolumeSettings()
    {
        var original = new UserSettings();
        original.OutputVolumes.Add(new OutputVolumeSetting { FriendlyName = "Speakers", Volume = 0.6, Muted = true, DelayMs = 15 });

        var json = JsonSerializer.Serialize(original, UserSettingsJsonContext.Default.UserSettings);
        var roundTripped = JsonSerializer.Deserialize(json, UserSettingsJsonContext.Default.UserSettings);

        var setting = Assert.Single(roundTripped!.OutputVolumes);
        Assert.Equal("Speakers", setting.FriendlyName);
        Assert.Equal(0.6, setting.Volume);
        Assert.True(setting.Muted);
        Assert.Equal(15, setting.DelayMs);
    }

    [Fact]
    public void RoundTrip_PreservesSubtitleZoneCount()
    {
        var original = new UserSettings();

        var json = JsonSerializer.Serialize(original, UserSettingsJsonContext.Default.UserSettings);
        var roundTripped = JsonSerializer.Deserialize(json, UserSettingsJsonContext.Default.UserSettings);

        Assert.Equal(original.SubtitleZones.Count, roundTripped!.SubtitleZones.Count);
    }
}
