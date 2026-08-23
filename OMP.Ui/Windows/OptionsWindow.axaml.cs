using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;
using OMP.Ui.Helpers;
using OMP.Ui.Localization;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;

namespace OMP.Ui.Windows;

public sealed partial class OptionsWindow : Window
{
    private static readonly FilePickerFileType _ytDlpFileTypeFilter = new(Strings.Options_YtDlpPathFileTypeFilterName)
    {
        Patterns = OperatingSystem.IsWindows() ? ["*.exe"] : ["*"]
    };

    private static readonly FilePickerFileType _subtitleFileTypeFilter = new(Strings.Options_SubtitleFileTypeFilterName)
    {
        Patterns = ["*.srt", "*.vtt", "*.ass", "*.ssa", "*.sub"]
    };

    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IUserSettingsService _settings;
    private readonly IWindowFactory _windowFactory;
    private readonly ILogger<OptionsWindow> _logger;
    private readonly ObservableCollection<AudioRouteRow> _audioRouteRows = [];
    private readonly ObservableCollection<SubtitleZone> _subtitleZones = [];
    private readonly ObservableCollection<SubtitleRouteRow> _subtitleRows = [];

    private readonly List<AudioStreamOption> _streamOptions = [];
    private readonly List<AudioOutput> _outputs = [];
    private readonly List<SubtitleStreamOption> _subtitleStreamOptions = [];

    public OptionsWindow(IMediaSessionRegistry mediaSessionRegistry, IUserSettingsService settings,
        IWindowFactory windowFactory, ILogger<OptionsWindow> logger)
    {
        InitializeComponent();

        _mediaSessionRegistry = mediaSessionRegistry;
        _settings = settings;
        _windowFactory = windowFactory;
        _logger = logger;

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

        YtDlpPathTextBox.Text = _settings.Current.YtDlpPath ?? string.Empty;

        RoutesList.ItemsSource = _audioRouteRows;
        StreamSelector.ItemsSource = _streamOptions;
        ZonesList.ItemsSource = _subtitleZones;
        SubtitleRoutesList.ItemsSource = _subtitleRows;

        ClearDraftRouteButton.Click += OnClearDraftRoute;
        AddZoneButton.Click += OnAddZone;
        ClearDraftSubtitleRouteButton.Click += OnClearDraftSubtitleRoute;
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

    private void OnYtDlpPathChanged(object? sender, RoutedEventArgs e)
    {
        var text = YtDlpPathTextBox.Text?.Trim();
        _settings.Current.YtDlpPath = string.IsNullOrEmpty(text) ? null : text;
        _settings.Save();
    }

    private void OnResetYtDlpPath(object? sender, RoutedEventArgs e)
    {
        YtDlpPathTextBox.Text = string.Empty;
        _settings.Current.YtDlpPath = null;
        _settings.Save();
    }

    private async void OnBrowseYtDlpPath(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = Strings.Options_YtDlpPathBrowseTitle,
                AllowMultiple = false,
                FileTypeFilter = [_ytDlpFileTypeFilter]
            });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();

        if (path == null)
        {
            return;
        }

        YtDlpPathTextBox.Text = path;
        _settings.Current.YtDlpPath = path;
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

    private void OnDraftOutputChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (OutputSelector.SelectedItem is not AudioOutput)
        {
            StreamSelector.IsEnabled = false;
            StreamSelector.SelectedItem = null;
            return;
        }

        StreamSelector.IsEnabled = true;
        if (_streamOptions.Count == 1)
        {
            StreamSelector.SelectedIndex = 0;
        }

        TryCommitDraftRoute();
    }

    private void OnDraftStreamChanged(object? sender, SelectionChangedEventArgs e) => TryCommitDraftRoute();

    private void OnClearDraftRoute(object? sender, RoutedEventArgs e) => OutputSelector.SelectedItem = null;

    private void TryCommitDraftRoute()
    {
        if (OutputSelector.SelectedItem is not AudioOutput output ||
            StreamSelector.SelectedItem is not AudioStreamOption streamOption)
        {
            return;
        }

        var savedDelayMs = _settings.Current.OutputVolumes
            .FirstOrDefault(o => o.FriendlyName == output.FriendlyName)?.DelayMs ?? 0;

        _audioRouteRows.Add(
            new AudioRouteRow(new AudioRoute(streamOption.Stream, output), volume: 100, muted: false, savedDelayMs));
        UpdateRowStreamOptions();
        RefreshRows();
        ApplyAndPersistRoutes();

        UpdateOutputSelector();
        OutputSelector.Focus();
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
        if (((Control)sender!).DataContext is not AudioRouteRow || e.RemovedItems.Count == 0)
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

    private void OnDraftSubtitleStreamChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SubtitleStreamSelector.SelectedItem is not SubtitleStreamOption { IsSupported: true })
        {
            SubtitleZoneSelector.IsEnabled = false;
            SubtitleZoneSelector.SelectedItem = null;
            return;
        }

        SubtitleZoneSelector.IsEnabled = true;
        if (SubtitleZoneSelector.Items.Count == 1)
        {
            SubtitleZoneSelector.SelectedIndex = 0;
        }

        TryCommitDraftSubtitleRoute();
    }

    private void OnDraftSubtitleZoneChanged(object? sender, SelectionChangedEventArgs e) =>
        TryCommitDraftSubtitleRoute();

    private void OnClearDraftSubtitleRoute(object? sender, RoutedEventArgs e) =>
        SubtitleStreamSelector.SelectedItem = null;

    private void TryCommitDraftSubtitleRoute()
    {
        if (SubtitleStreamSelector.SelectedItem is not SubtitleStreamOption { IsSupported: true } streamOption ||
            SubtitleZoneSelector.SelectedItem is not SubtitleZone zone)
        {
            return;
        }

        SubtitleRouteErrorText.IsVisible = false;
        _subtitleRows.Add(new SubtitleRouteRow(streamOption.Stream, zone));
        ApplySubtitleRoutes();

        UpdateSubtitleStreamSelector();
        UpdateSubtitleZoneSelector();
        SubtitleStreamSelector.Focus();
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

    private async void OnLoadSubtitleFile(object? sender, RoutedEventArgs e)
    {
        var session = _mediaSessionRegistry.Current;
        if (session is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = Strings.Options_LoadSubtitleFileTitle,
                AllowMultiple = false,
                FileTypeFilter = [_subtitleFileTypeFilter]
            });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (path is null)
        {
            return;
        }

        try
        {
            var sidecar = new SubtitleSidecarSource(path, Title: Path.GetFileNameWithoutExtension(path));
            var added = await Task.Run(() => session.AddSubtitleSidecar(sidecar));

            _subtitleStreamOptions.Add(new SubtitleStreamOption(added));
            UpdateSubtitleStreamSelector();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load subtitle file {Path}.", path);

            var errorWindow = _windowFactory.Create<OpenFileErrorWindow>();
            errorWindow.Load(Strings.OpenFileError_SubtitleHeading, ex.Message);
            await errorWindow.ShowDialog(this);
        }
    }

    private void ApplySubtitleRoutes()
    {
        var session = _mediaSessionRegistry.Current;
        if (session is null)
        {
            return;
        }

        var routes = _subtitleRows.Select(row => new SubtitleRoute(row.Stream, row.Zone.Id)).ToList();
        _ = Task.Run(() =>
        {
            try
            {
                var applied = session.SetSubtitleRoutes(routes);
                Dispatcher.UIThread.Post(() => ReconcileSubtitleRoutes(applied));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Applying subtitle routes failed.");
            }
        });
    }

    private void ReconcileSubtitleRoutes(IReadOnlyList<SubtitleRoute> applied)
    {
        var failedRows = _subtitleRows
            .Where(row => !applied.Any(r => r.Stream.Id == row.Stream.Id && r.ZoneId == row.Zone.Id))
            .ToList();

        if (failedRows.Count == 0)
        {
            return;
        }

        foreach (var row in failedRows)
        {
            _subtitleRows.Remove(row);
        }

        UpdateSubtitleStreamSelector();
        UpdateSubtitleZoneSelector();

        SubtitleRouteErrorText.Text = Strings.Options_SubtitleRouteError;
        SubtitleRouteErrorText.IsVisible = true;
    }

    private void UpdateSubtitleStreamSelector()
    {
        var availableStreams = OptionsSelector.AvailableOptions(
            _subtitleStreamOptions, _subtitleRows.Select(row => row.Stream.Id), o => o.Stream.Id);

        SubtitleStreamSelector.ItemsSource = availableStreams;

        if (SubtitleStreamSelector.SelectedItem is SubtitleStreamOption selected &&
            !availableStreams.Contains(selected))
        {
            SubtitleStreamSelector.SelectedItem = null;
        }
    }

    private void UpdateSubtitleZoneSelector()
    {
        var availableZones = OptionsSelector.AvailableOptions(
            _subtitleZones, _subtitleRows.Select(row => row.Zone.Id), z => z.Id);

        SubtitleZoneSelector.ItemsSource = availableZones;

        if (SubtitleZoneSelector.SelectedItem is SubtitleZone selected && !availableZones.Contains(selected))
        {
            SubtitleZoneSelector.SelectedItem = null;
        }
    }

    private void ApplyAndPersistRoutes()
    {
        var session = _mediaSessionRegistry.Current;
        if (session != null)
        {
            var routes = _audioRouteRows.Select(row => row.Route).ToList();
            _ = Task.Run(() =>
            {
                try
                {
                    session.SetAudioRoutes(routes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Applying audio routes failed.");
                }
            });
        }

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
        var availableOutputs = OptionsSelector.AvailableOptions(
            _outputs, _audioRouteRows.Select(row => row.Route.Output.FriendlyName), o => o.FriendlyName);

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