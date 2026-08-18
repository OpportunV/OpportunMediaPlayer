using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;
using OMP.Ui.Localization;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Windows;

public sealed partial class OptionsWindow : Window
{
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IUserSettingsService _settings;
    private readonly IWindowFactory _windowFactory;
    private readonly ObservableCollection<AudioRouteRow> _audioRouteRows = [];
    private readonly ObservableCollection<SubtitleZone> _subtitleZones = [];
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

            var delayMs = session.OutputDelays.TryGetValue(route.Output.Id, out var delaySeconds)
                ? delaySeconds * 1000
                : 0;

            _audioRouteRows.Add(new AudioRouteRow(route, volume.Volume * 100, volume.Muted, delayMs));
        }

        foreach (var zone in _settings.Current.SubtitleZones)
        {
            _subtitleZones.Add(zone.Clone());
        }

        _subtitleStreamOptions.AddRange((session?.SubtitleStreams ?? []).Select(s => new SubtitleStreamOption(s)));

        foreach (var route in session?.SubtitleRoutes ?? [])
        {
            var zone = _subtitleZones.FirstOrDefault(z => z.Id == route.ZoneId);
            if (zone is not null)
            {
                _subtitleRows.Add(new SubtitleRouteRow(route.Stream, zone));
            }
        }

        ThemeSelector.ItemsSource = Enum.GetValues<ThemeMode>().Select(mode => new ThemeModeOption(mode)).ToList();
        ThemeSelector.SelectedItem = ThemeSelector.Items
            .Cast<ThemeModeOption>()
            .First(option => option.Mode == _settings.Current.Theme);

        var languageOptions = new List<LanguageOption> { new(null, Strings.Common_SystemDefault) };
        languageOptions.AddRange(AvailableLanguages.Cultures
                .OrderBy(culture => culture.NativeName)
                .Select(culture => new LanguageOption(culture.Name, culture.NativeName)));

        LanguageSelector.ItemsSource = languageOptions;
        LanguageSelector.SelectedItem = languageOptions
            .FirstOrDefault(option => option.CultureCode == _settings.Current.Language) ?? languageOptions[0];

        RoutesList.ItemsSource = _audioRouteRows;
        StreamSelector.ItemsSource = _streamOptions;
        ZonesList.ItemsSource = _subtitleZones;
        SubtitleRoutesList.ItemsSource = _subtitleRows;

        AddRouteButton.Click += OnAddRouteButton;
        AddZoneButton.Click += OnAddZone;
        AddSubtitleRouteButton.Click += OnAddSubtitleRoute;
        UpdateOutputSelector();
        UpdateRowStreamOptions();
        UpdateSubtitleStreamSelector();
        UpdateSubtitleZoneSelector();
        RefreshRows();
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ThemeSelector.SelectedItem is not ThemeModeOption option)
        {
            return;
        }

        _settings.Current.Theme = option.Mode;
        _settings.Save();

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = option.Mode.ToThemeVariant();
        }
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguageSelector.SelectedItem is not LanguageOption option)
        {
            return;
        }

        if (option.CultureCode != _settings.Current.Language)
        {
            RestartNowButton.IsVisible = true;
        }

        _settings.Current.Language = option.CultureCode;
        _settings.Save();
    }

    private void OnRestartNowClick(object? sender, RoutedEventArgs e)
    {
        var exePath = Environment.GetEnvironmentVariable("APPIMAGE") ?? Environment.ProcessPath;

        if (exePath is not null)
        {
            var startInfo = new ProcessStartInfo(exePath);

            if (_mediaSessionRegistry.Current?.FilePath is { } filePath)
            {
                startInfo.ArgumentList.Add(filePath);
            }

            Process.Start(startInfo);
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }

    private void OnAddRouteButton(object? sender, RoutedEventArgs e)
    {
        if (StreamSelector.SelectedItem is not AudioStreamOption streamOption ||
            OutputSelector.SelectedItem is not AudioOutput output)
        {
            return;
        }

        var savedDelayMs = _settings.Current.OutputVolumes
            .FirstOrDefault(o => o.FriendlyName == output.FriendlyName)?.DelayMs ?? 0;

        _audioRouteRows.Add(
            new AudioRouteRow(new AudioRoute(streamOption.Stream, output), volume: 100, muted: false, savedDelayMs));
        UpdateOutputSelector();
        UpdateRowStreamOptions();
        RefreshRows();
        ApplyAndPersistRoutes();
    }

    private void OnDeleteRoute(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not AudioRouteRow row || _audioRouteRows.Count <= 1)
        {
            return;
        }

        _audioRouteRows.Remove(row);
        UpdateOutputSelector();
        UpdateRowStreamOptions();
        RefreshRows();
        ApplyAndPersistRoutes();
    }

    private void OnRouteStreamChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (((Control)sender!).DataContext is not AudioRouteRow)
        {
            return;
        }

        ApplyAndPersistRoutes();
    }

    private void OnRouteVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (((Control)sender!).DataContext is not AudioRouteRow row)
        {
            return;
        }

        _mediaSessionRegistry.Current?.SetOutputVolume(row.Route.Output.Id, e.NewValue / 100);
        _settings.UpsertOutputVolumeSetting(row.Route.Output, e.NewValue, row.Muted, row.DelayMs);
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
        _settings.UpsertOutputVolumeSetting(row.Route.Output, row.Volume, muted, row.DelayMs);

        _settings.Save();
    }

    private void OnRouteDelayChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (((Control)sender!).DataContext is not AudioRouteRow row)
        {
            return;
        }

        var delayMs = (double)(e.NewValue ?? 0);
        _mediaSessionRegistry.Current?.SetOutputDelay(row.Route.Output.Id, delayMs / 1000.0);
        _settings.UpsertOutputVolumeSetting(row.Route.Output, row.Volume, row.Muted, delayMs);

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

        _subtitleZones.Add(result);
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

        var index = _subtitleZones.IndexOf(zone);
        if (index >= 0)
        {
            _subtitleZones[index] = result;
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

        var index = _subtitleZones.IndexOf(zone);
        if (index < 0)
        {
            return;
        }

        _subtitleZones[index] = SubtitleZone.CreateBuiltIns().First(z => z.Id == zone.Id);
        PersistZones();
        UpdateSubtitleZoneSelector();
    }

    private void OnDeleteZone(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleZone zone || zone.IsBuiltIn)
        {
            return;
        }

        _subtitleZones.Remove(zone);
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

        if (SubtitleStreamSelector.SelectedItem is SubtitleStreamOption selected &&
            !availableStreams.Contains(selected))
        {
            SubtitleStreamSelector.SelectedItem = null;
        }
    }

    private void UpdateSubtitleZoneSelector()
    {
        var usedZoneIds = _subtitleRows.Select(row => row.Zone.Id).ToHashSet();

        var availableZones = _subtitleZones
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
        _mediaSessionRegistry.Current?.SetAudioRoutes(_audioRouteRows.Select(row => row.Route));

        _settings.Current.PreferredAudioTracks = _audioRouteRows
            .Select(row => new PreferredAudioTrackSetting
            {
                OutputFriendlyName = row.Route.Output.FriendlyName,
                Title = row.Route.Stream.Title,
                Language = row.Route.Stream.Language
            })
            .ToList();

        foreach (var row in _audioRouteRows)
        {
            _settings.UpsertOutputVolumeSetting(row.Route.Output, row.Volume, row.Muted, row.DelayMs);
        }

        _settings.Save();
    }

    private void PersistZones()
    {
        _settings.Current.SubtitleZones = _subtitleZones.ToList();
        _settings.Save();
    }

    private void UpdateOutputSelector()
    {
        var usedOutputs = _audioRouteRows.Select(row => row.Route.Output.FriendlyName).ToHashSet();

        var availableOutputs = _outputs
            .Where(o => !usedOutputs.Contains(o.FriendlyName))
            .ToList();

        OutputSelector.ItemsSource = availableOutputs;

        if (OutputSelector.SelectedItem is AudioOutput selected && !availableOutputs.Contains(selected))
        {
            OutputSelector.SelectedItem = null;
        }
    }

    private void UpdateRowStreamOptions()
    {
        foreach (var row in _audioRouteRows)
        {
            row.AvailableStreamOptions = _streamOptions;
        }
    }

    private void RefreshRows()
    {
        var canDelete = _audioRouteRows.Count > 1;
        var snapshot = _audioRouteRows.ToList();

        foreach (var row in snapshot)
        {
            row.CanDelete = canDelete;
        }

        _audioRouteRows.Clear();

        foreach (var row in snapshot)
        {
            _audioRouteRows.Add(row);
        }
    }
}