using Avalonia.Controls;
using OMP.Lib.Session;
using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui.Services;

/// <summary>
/// The master volume slider, its readout, and the speaker/muted icon pair on the playback bar.
/// Also restores persisted master and per-output levels onto a newly opened session.
/// </summary>
internal sealed class VolumeBarPresenter
{
    public bool IsMuted
    {
        get;
        set
        {
            field = value;
            _settings.Current.IsMuted = value;
            UpdateMuteIcon();
        }
    }

    private readonly Slider _volumeSlider;
    private readonly TextBlock _volumeLabel;
    private readonly Control _speakerIcon;
    private readonly Control _speakerMutedIcon;
    private readonly IUserSettingsService _settings;

    public VolumeBarPresenter(
        Slider volumeSlider,
        TextBlock volumeLabel,
        Control speakerIcon,
        Control speakerMutedIcon,
        IMainWindowCommands commands,
        IUserSettingsService settings)
    {
        _volumeSlider = volumeSlider;
        _volumeLabel = volumeLabel;
        _speakerIcon = speakerIcon;
        _speakerMutedIcon = speakerMutedIcon;
        _settings = settings;

        _volumeSlider.Value = _settings.Current.MasterVolume * 100;
        _volumeLabel.Text = FormatPercent(_volumeSlider.Value);
        IsMuted = _settings.Current.IsMuted;

        _volumeSlider.ValueChanged += (_, e) =>
        {
            commands.SetMasterVolume(e.NewValue / 100);
            _settings.Current.MasterVolume = e.NewValue / 100;
            _volumeLabel.Text = FormatPercent(e.NewValue);
        };

        _volumeSlider.PointerCaptureLost += (_, _) => _settings.Save();
    }

    public void OnVolumeChanged(double volume)
    {
        _volumeSlider.Value = volume * 100;
        _volumeLabel.Text = FormatPercent(_volumeSlider.Value);
        _settings.Current.MasterVolume = volume;
        _settings.Save();
    }

    public void RestoreVolume(IMediaSession session)
    {
        session.SetMasterVolume(_settings.Current.MasterVolume);
        session.SetMasterMuted(_settings.Current.IsMuted);
        IsMuted = _settings.Current.IsMuted;

        foreach (var (output, setting) in session.AudioOutputs.MatchSettings(_settings.Current.OutputVolumes))
        {
            session.SetOutputVolume(output.Id, setting.Volume);
            session.SetOutputMuted(output.Id, setting.Muted);
            session.SetOutputDelay(output.Id, setting.DelayMs / 1000.0);
        }
    }

    private static string FormatPercent(double value) => $"{(int)value}%";

    private void UpdateMuteIcon()
    {
        _speakerIcon.IsVisible = !IsMuted;
        _speakerMutedIcon.IsVisible = IsMuted;
    }
}
