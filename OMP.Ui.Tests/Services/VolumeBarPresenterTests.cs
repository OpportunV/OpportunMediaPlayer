using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Moq;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Ui.Services;
using OMP.Ui.Settings;
using OMP.Ui.Tests.TestDoubles;

namespace OMP.Ui.Tests.Services;

public class VolumeBarPresenterTests
{
    private static readonly AudioStream _stream = new(1, "aac", "Main", "en");
    private static readonly AudioOutput _speakers = new(1, "Speakers");

    [AvaloniaFact]
    public void SeedsTheSliderAndReadoutFromPersistedVolume()
    {
        var h = new Harness(masterVolume: 0.75);

        Assert.Equal(75, h.Slider.Value);
        Assert.Equal("75%", h.Label.Text);
    }

    [AvaloniaFact]
    public void SeedsTheMuteIconsFromPersistedMuteState()
    {
        var muted = new Harness(isMuted: true);
        var unmuted = new Harness(isMuted: false);

        Assert.False(muted.SpeakerIcon.IsVisible);
        Assert.True(muted.SpeakerMutedIcon.IsVisible);

        Assert.True(unmuted.SpeakerIcon.IsVisible);
        Assert.False(unmuted.SpeakerMutedIcon.IsVisible);
    }

    [AvaloniaFact]
    public void DraggingTheSlider_PushesTheVolumeAndUpdatesTheReadout()
    {
        var h = new Harness();

        h.Slider.Value = 40;

        Assert.Equal(0.4, h.Commands.LastMasterVolume);
        Assert.Equal("40%", h.Label.Text);
        Assert.Equal(0.4, h.Settings.MasterVolume);
    }

    /// <summary>
    /// The path a volume hotkey takes: the engine already changed, and the bar has to catch up.
    /// If this were unwired the audio would still respond while the UI silently froze.
    /// </summary>
    [AvaloniaFact]
    public void OnVolumeChanged_MovesTheSliderAndReadoutToMatch()
    {
        var h = new Harness();

        h.Presenter.OnVolumeChanged(0.25);

        Assert.Equal(25, h.Slider.Value);
        Assert.Equal("25%", h.Label.Text);
        Assert.Equal(0.25, h.Settings.MasterVolume);
    }

    [AvaloniaFact]
    public void SettingIsMuted_SwapsTheIconsAndPersists()
    {
        var h = new Harness(isMuted: false);

        h.Presenter.IsMuted = true;

        Assert.False(h.SpeakerIcon.IsVisible);
        Assert.True(h.SpeakerMutedIcon.IsVisible);
        Assert.True(h.Settings.IsMuted);
    }

    [AvaloniaFact]
    public void RestoreVolume_AppliesMasterAndPerOutputSettingsToTheSession()
    {
        var h = new Harness(masterVolume: 0.6, isMuted: true);
        h.Settings.OutputVolumes =
        [
            new OutputVolumeSetting { FriendlyName = "Speakers", Volume = 0.3, Muted = true, DelayMs = 250 }
        ];

        var session = new FakeMediaSession
        {
            AudioOutputs = [_speakers],
            AudioRoutes = [new AudioRoute(_stream, _speakers)]
        };

        h.Presenter.RestoreVolume(session);

        Assert.Equal(0.6, session.MasterVolume);
        Assert.True(session.IsMuted);
        Assert.Contains(session.OutputVolumeCalls, c => c.OutputId == _speakers.Id && Math.Abs(c.Volume - 0.3) < 0.001);
        Assert.Contains(session.OutputMutedCalls, c => c.OutputId == _speakers.Id && c.Muted);
        Assert.Contains(session.OutputDelayCalls, c => c.OutputId == _speakers.Id && Math.Abs(c.DelaySeconds - 0.25) < 0.001);
    }

    private sealed class Harness
    {
        public Slider Slider { get; } = new() { Minimum = 0, Maximum = 200 };

        public TextBlock Label { get; } = new();

        public Control SpeakerIcon { get; } = new Border();

        public Control SpeakerMutedIcon { get; } = new Border();

        public UserSettings Settings { get; } = new();

        public RecordingCommands Commands { get; } = new();

        public VolumeBarPresenter Presenter { get; }

        public Harness(double masterVolume = 1.0, bool isMuted = false)
        {
            Settings.MasterVolume = masterVolume;
            Settings.IsMuted = isMuted;

            var settingsService = new Mock<IUserSettingsService>();
            settingsService.Setup(s => s.Current).Returns(Settings);

            Presenter = new VolumeBarPresenter(
                Slider, Label, SpeakerIcon, SpeakerMutedIcon, Commands, settingsService.Object);
        }
    }
}
