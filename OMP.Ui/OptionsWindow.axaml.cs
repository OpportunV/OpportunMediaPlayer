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
using OMP.Lib.Subtitle;
using OMP.Ui.Controls;
using OMP.Ui.Models;
using OMP.Ui.Settings;

namespace OMP.Ui;

public sealed partial class OptionsWindow : Window
{
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IUserSettingsService _settings;
    private readonly IWindowFactory _windowFactory;
    private readonly ObservableCollection<AudioRouteRow> _rows = [];
    private readonly ObservableCollection<SubtitleZone> _zones = [];
    private readonly ObservableCollection<SubtitleRouteRow> _subtitleRows = [];

    private readonly List<AudioStreamOption> _streamOptions = [];
    private readonly List<AudioOutput> _outputs = [];
    private readonly List<SubtitleStreamOption> _subtitleStreamOptions = [];

    public OptionsWindow(IMediaSessionRegistry mediaSessionRegistry, IUserSettingsService settings,
        IWindowFactory windowFactory)
    {
        InitializeComponent();

        _mediaSessionRegistry = mediaSessionRegistry;
        _settings = settings;
        _windowFactory = windowFactory;

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

        foreach (var zone in _settings.Current.SubtitleZones)
        {
            _zones.Add(zone.Clone());
        }

        _subtitleStreamOptions.AddRange((session?.SubtitleStreams ?? []).Select(s => new SubtitleStreamOption(s)));

        foreach (var route in session?.SubtitleRoutes ?? [])
        {
            var zone = _zones.FirstOrDefault(z => z.Id == route.ZoneId);
            if (zone is not null)
            {
                _subtitleRows.Add(new SubtitleRouteRow(route.Stream, zone));
            }
        }

        RoutesList.ItemsSource = _rows;
        StreamSelector.ItemsSource = _streamOptions;
        ZonesList.ItemsSource = _zones;
        SubtitleRoutesList.ItemsSource = _subtitleRows;

        AddRouteButton.Click += OnAddRouteButton;
        AddZoneButton.Click += OnAddZone;
        AddSubtitleRouteButton.Click += OnAddSubtitleRoute;
        UpdateOutputSelector();
        UpdateSubtitleStreamSelector();
        UpdateSubtitleZoneSelector();
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
        ApplyAndPersistRoutes();
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
        ApplyAndPersistRoutes();
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

    private async void OnAddZone(object? sender, RoutedEventArgs e)
    {
        var editor = _windowFactory.Create<SubtitleZoneEditorWindow>();
        editor.Load(new SubtitleZone(), isNew: true);

        var result = await editor.ShowDialog<SubtitleZone?>(this);
        if (result is null)
        {
            return;
        }

        _zones.Add(result);
        PersistZones();
        UpdateSubtitleZoneSelector();
    }

    private async void OnEditZone(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleZone zone)
        {
            return;
        }

        var editor = _windowFactory.Create<SubtitleZoneEditorWindow>();
        editor.Load(zone.Clone(), isNew: false);

        var result = await editor.ShowDialog<SubtitleZone?>(this);
        if (result is null)
        {
            return;
        }

        var index = _zones.IndexOf(zone);
        if (index >= 0)
        {
            _zones[index] = result;
            PersistZones();
            UpdateSubtitleZoneSelector();
        }
    }

    private void OnResetZone(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleZone { IsBuiltIn: true } zone)
        {
            return;
        }

        var index = _zones.IndexOf(zone);
        if (index < 0)
        {
            return;
        }

        _zones[index] = SubtitleZone.CreateBuiltIns().First(z => z.Id == zone.Id);
        PersistZones();
        UpdateSubtitleZoneSelector();
    }

    private void OnDeleteZone(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleZone zone || zone.IsBuiltIn)
        {
            return;
        }

        _zones.Remove(zone);
        PersistZones();
        UpdateSubtitleZoneSelector();

        var orphanedRows = _subtitleRows.Where(row => row.Zone.Id == zone.Id).ToList();
        if (orphanedRows.Count == 0)
        {
            return;
        }

        foreach (var row in orphanedRows)
        {
            _subtitleRows.Remove(row);
        }

        UpdateSubtitleStreamSelector();
        ApplySubtitleRoutes();
    }

    private void OnAddSubtitleRoute(object? sender, RoutedEventArgs e)
    {
        if (SubtitleStreamSelector.SelectedItem is not SubtitleStreamOption { IsSupported: true } streamOption ||
            SubtitleZoneSelector.SelectedItem is not SubtitleZone zone)
        {
            return;
        }

        _subtitleRows.Add(new SubtitleRouteRow(streamOption.Stream, zone));
        UpdateSubtitleStreamSelector();
        UpdateSubtitleZoneSelector();
        ApplySubtitleRoutes();
    }

    private void OnDeleteSubtitleRoute(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleRouteRow row)
        {
            return;
        }

        _subtitleRows.Remove(row);
        UpdateSubtitleStreamSelector();
        UpdateSubtitleZoneSelector();
        ApplySubtitleRoutes();
    }

    private void ApplySubtitleRoutes()
    {
        _mediaSessionRegistry.Current?.SetSubtitleRoutes(
            _subtitleRows.Select(row => new SubtitleRoute(row.Stream, row.Zone.Id)));
    }

    private void UpdateSubtitleStreamSelector()
    {
        var usedStreamIds = _subtitleRows.Select(row => row.Stream.Id).ToHashSet();

        var availableStreams = _subtitleStreamOptions
            .Where(o => !usedStreamIds.Contains(o.Stream.Id))
            .ToList();

        SubtitleStreamSelector.ItemsSource = availableStreams;

        if (SubtitleStreamSelector.SelectedItem is SubtitleStreamOption selected && !availableStreams.Contains(selected))
        {
            SubtitleStreamSelector.SelectedItem = null;
        }
    }

    private void UpdateSubtitleZoneSelector()
    {
        var usedZoneIds = _subtitleRows.Select(row => row.Zone.Id).ToHashSet();

        var availableZones = _zones
            .Where(z => !usedZoneIds.Contains(z.Id))
            .ToList();

        SubtitleZoneSelector.ItemsSource = availableZones;

        if (SubtitleZoneSelector.SelectedItem is SubtitleZone selected && !availableZones.Contains(selected))
        {
            SubtitleZoneSelector.SelectedItem = null;
        }
    }

    private void ApplyAndPersistRoutes()
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
    }

    private void PersistZones()
    {
        _settings.Current.SubtitleZones = _zones.ToList();
        _settings.Save();
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
