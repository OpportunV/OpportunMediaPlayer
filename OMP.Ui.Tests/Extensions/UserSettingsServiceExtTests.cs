using Moq;
using OMP.Lib.Audio.Output;
using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui.Tests.Extensions;

public class UserSettingsServiceExtTests
{
    [Fact]
    public void UpsertOutputVolumeSetting_NewOutput_AddsSetting()
    {
        var settings = new UserSettings();
        var service = new Mock<IUserSettingsService>();
        service.Setup(s => s.Current).Returns(settings);
        var output = new AudioOutput(1, "Speakers");

        service.Object.UpsertOutputVolumeSetting(output, 75, false);

        var setting = Assert.Single(settings.OutputVolumes);
        Assert.Equal("Speakers", setting.FriendlyName);
        Assert.Equal(0.75, setting.Volume);
        Assert.False(setting.Muted);
    }

    [Fact]
    public void UpsertOutputVolumeSetting_ExistingOutput_UpdatesInPlace()
    {
        var settings = new UserSettings();
        settings.OutputVolumes.Add(new OutputVolumeSetting { FriendlyName = "Speakers", Volume = 0.5, Muted = false });
        var service = new Mock<IUserSettingsService>();
        service.Setup(s => s.Current).Returns(settings);
        var output = new AudioOutput(1, "Speakers");

        service.Object.UpsertOutputVolumeSetting(output, 40, true);

        var setting = Assert.Single(settings.OutputVolumes);
        Assert.Equal(0.4, setting.Volume);
        Assert.True(setting.Muted);
    }

    [Fact]
    public void UpsertOutputVolumeSetting_DelayNotProvided_LeavesExistingDelayUnchanged()
    {
        var settings = new UserSettings();
        settings.OutputVolumes.Add(new OutputVolumeSetting { FriendlyName = "Speakers", DelayMs = 120 });
        var service = new Mock<IUserSettingsService>();
        service.Setup(s => s.Current).Returns(settings);
        var output = new AudioOutput(1, "Speakers");

        service.Object.UpsertOutputVolumeSetting(output, 100, false);

        Assert.Equal(120, settings.OutputVolumes[0].DelayMs);
    }

    [Fact]
    public void UpsertOutputVolumeSetting_DelayProvided_UpdatesDelay()
    {
        var settings = new UserSettings();
        var service = new Mock<IUserSettingsService>();
        service.Setup(s => s.Current).Returns(settings);
        var output = new AudioOutput(1, "Speakers");

        service.Object.UpsertOutputVolumeSetting(output, 100, false, delayMs: 250);

        Assert.Equal(250, settings.OutputVolumes[0].DelayMs);
    }
}
