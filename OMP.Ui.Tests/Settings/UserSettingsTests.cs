using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Settings;

public class UserSettingsTests
{
    [Fact]
    public void Constructor_DefaultsToVersionOneUnmutedFullVolumeNormalSpeed()
    {
        var settings = new UserSettings();

        Assert.Equal(UserSettings.CurrentVersion, settings.Version);
        Assert.Equal(1.0, settings.MasterVolume);
        Assert.False(settings.IsMuted);
        Assert.Equal(1.0, settings.PlaybackSpeed);
        Assert.Equal(ThemeMode.System, settings.Theme);
    }

    [Fact]
    public void Constructor_DefaultsToBuiltInSubtitleZones()
    {
        var settings = new UserSettings();

        Assert.Equal(2, settings.SubtitleZones.Count);
        Assert.All(settings.SubtitleZones, z => Assert.True(z.IsBuiltIn));
    }

    [Fact]
    public void Constructor_DefaultsToEmptyOutputAndTrackSettings()
    {
        var settings = new UserSettings();

        Assert.Empty(settings.OutputVolumes);
        Assert.Empty(settings.PreferredAudioTracks);
    }
}
