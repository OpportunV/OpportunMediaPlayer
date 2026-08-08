using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Session;
using OMP.Ui.Models;
using OMP.Ui.Settings;

namespace OMP.Ui;

public sealed partial class OptionsWindow : Window
{
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IUserSettingsService _settings;
    private readonly ObservableCollection<AudioRouteRow> _rows = [];

    private readonly List<AudioStreamOption> _streamOptions = [];
    private readonly List<AudioOutput> _outputs = [];

    public OptionsWindow(IMediaSessionRegistry mediaSessionRegistry, IUserSettingsService settings)
    {
        InitializeComponent();

        _mediaSessionRegistry = mediaSessionRegistry;
        _settings = settings;

        var session = _mediaSessionRegistry.Current;
        _streamOptions.AddRange((session?.AudioStreams ?? []).Select(stream => new AudioStreamOption(stream)));
        _outputs.AddRange(session?.AudioOutputs ?? []);

        foreach (var route in session?.AudioRoutes ?? [])
        {
            var volume = session!.OutputVolumes.TryGetValue(route.Output.Id, out var state)
                ? state
                : new OutputVolumeState(1.0, false);

            _rows.Add(new AudioRouteRow(route, volume.Volume * 100, volume.Muted));
        }

        RoutesList.ItemsSource = _rows;
        StreamSelector.ItemsSource = _streamOptions;

        AddRouteButton.Click += OnAddRouteButton;
        SaveButton.Click += OnSaveButton;
        UpdateOutputSelector();
        RefreshRows();
    }

    private void OnAddRouteButton(object? sender, RoutedEventArgs e)
    {
        if (StreamSelector.SelectedItem is not AudioStreamOption streamOption ||
            OutputSelector.SelectedItem is not AudioOutput output)
        {
            return;
        }

        _rows.Add(new AudioRouteRow(new AudioRoute(streamOption.Stream, output), volume: 100, muted: false));
        UpdateOutputSelector();
        RefreshRows();
    }

    private void OnDeleteRoute(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not AudioRouteRow row || _rows.Count <= 1)
        {
            return;
        }

        _rows.Remove(row);
        UpdateOutputSelector();
        RefreshRows();
    }

    private void OnRouteVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (((Control)sender!).DataContext is not AudioRouteRow row)
        {
            return;
        }

        _mediaSessionRegistry.Current?.SetOutputVolume(row.Route.Output.Id, e.NewValue / 100);
        UpsertOutputVolumeSetting(row.Route.Output, e.NewValue, row.Muted);
    }

    private void OnRouteVolumeReleased(object? sender, PointerCaptureLostEventArgs e)
    {
        _settings.Save();
    }

    private void OnRouteMuteChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { DataContext: AudioRouteRow row } toggle)
        {
            return;
        }

        var muted = toggle.IsChecked == true;
        _mediaSessionRegistry.Current?.SetOutputMuted(row.Route.Output.Id, muted);
        UpsertOutputVolumeSetting(row.Route.Output, row.Volume, muted);

        _settings.Save();
    }

    private void OnSaveButton(object? sender, RoutedEventArgs e)
    {
        _mediaSessionRegistry.Current?.SetAudioRoutes(_rows.Select(row => row.Route));

        _settings.Current.PreferredAudioOutputs = _rows
            .Select(row => row.Route.Output.FriendlyName)
            .ToList();

        foreach (var row in _rows)
        {
            UpsertOutputVolumeSetting(row.Route.Output, row.Volume, row.Muted);
        }

        _settings.Save();
        Close(true);
    }

    private void UpsertOutputVolumeSetting(AudioOutput output, double volumePercent, bool muted)
    {
        var existing = _settings.Current.OutputVolumes
            .FirstOrDefault(o => o.FriendlyName == output.FriendlyName);

        if (existing is null)
        {
            existing = new OutputVolumeSetting { FriendlyName = output.FriendlyName };
            _settings.Current.OutputVolumes.Add(existing);
        }

        existing.Volume = volumePercent / 100;
        existing.Muted = muted;
    }

    private void UpdateOutputSelector()
    {
        var usedOutputs = _rows.Select(row => row.Route.Output.FriendlyName).ToHashSet();

        var availableOutputs = _outputs
            .Where(o => !usedOutputs.Contains(o.FriendlyName))
            .ToList();

        OutputSelector.ItemsSource = availableOutputs;

        if (OutputSelector.SelectedItem is AudioOutput selected && !availableOutputs.Contains(selected))
        {
            OutputSelector.SelectedItem = null;
        }
    }

    private void RefreshRows()
    {
        var canDelete = _rows.Count > 1;
        var snapshot = _rows.ToList();

        foreach (var row in snapshot)
        {
            row.CanDelete = canDelete;
        }

        _rows.Clear();

        foreach (var row in snapshot)
        {
            _rows.Add(row);
        }
    }
}
