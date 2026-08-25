using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Session;
using OMP.Ui.Extensions;
using OMP.Ui.Helpers;
using OMP.Ui.Models;
using OMP.Ui.Settings;

namespace OMP.Ui.Controls;

internal sealed partial class OptionsAudioRoutingTab : UserControl
{
    private readonly ObservableCollection<AudioRouteRow> _rows = [];
    private readonly List<AudioStreamOption> _streamOptions = [];
    private readonly List<AudioOutput> _outputs = [];

    private IMediaSessionRegistry _mediaSessionRegistry = null!;
    private IUserSettingsService _settings = null!;
    private ILogger _logger = null!;

    public OptionsAudioRoutingTab()
    {
        InitializeComponent();
    }

    public void Initialize(IMediaSessionRegistry mediaSessionRegistry, IUserSettingsService settings, ILoggerFactory loggerFactory)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        _settings = settings;
        _logger = loggerFactory.CreateLogger<OptionsAudioRoutingTab>();

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

            _rows.Add(new AudioRouteRow(route, volume.Volume * 100, volume.Muted, delayMs));
        }

        RoutesList.ItemsSource = _rows;
        StreamSelector.ItemsSource = _streamOptions;

        UpdateOutputSelector();
        UpdateRowStreamOptions();
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
        UpdateRowStreamOptions();
        RefreshRows();
        ApplyAndPersistRoutes();
    }

    /// <summary>
    /// An empty <see cref="SelectionChangedEventArgs.RemovedItems"/> means the container is syncing
    /// its initial selection rather than the user picking a different track, which would otherwise
    /// reapply every route once per row.
    /// </summary>
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

        _rows.Add(
            new AudioRouteRow(new AudioRoute(streamOption.Stream, output), volume: 100, muted: false, savedDelayMs));
        UpdateRowStreamOptions();
        RefreshRows();
        ApplyAndPersistRoutes();

        UpdateOutputSelector();
        OutputSelector.Focus();
    }

    private void ApplyAndPersistRoutes()
    {
        var session = _mediaSessionRegistry.Current;
        if (session != null)
        {
            var routes = _rows.Select(row => row.Route).ToList();
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

        _settings.Current.PreferredAudioTracks = _rows
            .Select(row => new PreferredAudioTrackSetting
            {
                OutputFriendlyName = row.Route.Output.FriendlyName,
                Title = row.Route.Stream.Title,
                Language = row.Route.Stream.Language
            })
            .ToList();

        foreach (var row in _rows)
        {
            _settings.UpsertOutputVolumeSetting(row.Route.Output, row.Volume, row.Muted, row.DelayMs);
        }

        _settings.Save();
    }

    private void UpdateOutputSelector() =>
        OptionsSelector.Rebind(
            OutputSelector, _outputs, _rows.Select(row => row.Route.Output.FriendlyName), o => o.FriendlyName);

    private void UpdateRowStreamOptions()
    {
        foreach (var row in _rows)
        {
            row.AvailableStreamOptions = _streamOptions;
        }
    }

    private void RefreshRows()
    {
        var canDelete = _rows.Count > 1;

        foreach (var row in _rows)
        {
            row.CanDelete = canDelete;
        }
    }
}
