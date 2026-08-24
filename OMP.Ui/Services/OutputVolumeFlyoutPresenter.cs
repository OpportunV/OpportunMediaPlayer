using Avalonia.Controls;
using OMP.Lib.Audio.Output;
using OMP.Lib.Session;
using OMP.Ui.Controls;
using OMP.Ui.Extensions;
using OMP.Ui.Settings;

namespace OMP.Ui.Services;

/// <summary>
/// The per-output volume flyout on the playback bar: one row per active audio route, each with its
/// own volume and mute. Rows are rebuilt every time the flyout opens rather than tracked, since
/// routes can change while it is closed.
/// </summary>
internal sealed class OutputVolumeFlyoutPresenter
{
    private readonly VolumeFlyoutView _view = new();
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IUserSettingsService _settings;

    public OutputVolumeFlyoutPresenter(
        Button outputVolumesButton,
        IMediaSessionRegistry mediaSessionRegistry,
        IUserSettingsService settings)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        _settings = settings;

        var flyout = new Flyout
        {
            Content = _view,
            Placement = PlacementMode.Top,
            FlyoutPresenterClasses = { "app-flyout" }
        };
        flyout.Opened += (_, _) => RefreshRows();
        outputVolumesButton.Flyout = flyout;

        _view.OutputVolumeChanged += (output, volume) =>
            _mediaSessionRegistry.Current?.SetOutputVolume(output.Id, volume / 100);

        _view.OutputVolumeCommitted += PersistOutputVolumeSetting;

        _view.OutputMuteChanged += (output, muted) =>
        {
            _mediaSessionRegistry.Current?.SetOutputMuted(output.Id, muted);
            PersistOutputVolumeSetting(output);
        };
    }

    private void RefreshRows()
    {
        var session = _mediaSessionRegistry.Current;

        if (session == null)
        {
            _view.SetOutputs([]);
            return;
        }

        _view.SetOutputs(session.AudioRoutes.ToVolumeRows(session.OutputVolumes));
    }

    private void PersistOutputVolumeSetting(AudioOutput output)
    {
        var session = _mediaSessionRegistry.Current;

        if (session != null && session.OutputVolumes.TryGetValue(output.Id, out var state))
        {
            _settings.UpsertOutputVolumeSetting(output, state.Volume * 100, state.Muted);
        }

        _settings.Save();
    }
}
