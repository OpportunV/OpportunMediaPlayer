using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;
using OMP.Ui.Helpers;
using OMP.Ui.Localization;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;
using OMP.Ui.Windows;

namespace OMP.Ui.Controls;

internal sealed partial class OptionsSubtitleRoutingTab : UserControl, IDisposable
{
    private static readonly FilePickerFileType _subtitleFileTypeFilter = new(Strings.Options_SubtitleFileTypeFilterName)
    {
        Patterns = ["*.srt", "*.vtt", "*.ass", "*.ssa", "*.sub"]
    };

    private readonly ObservableCollection<SubtitleRouteRow> _rows = [];
    private readonly List<SubtitleStreamOption> _streamOptions = [];

    private Window _owner = null!;
    private OptionsSubtitleZonesTab _zones = null!;
    private IMediaSessionRegistry _mediaSessionRegistry = null!;
    private IWindowFactory _windowFactory = null!;
    private IFilePickerService _filePicker = null!;
    private ISubtitleRouteApplier _routeApplier = null!;
    private ILogger _logger = null!;
    private int _applyCount;

    public OptionsSubtitleRoutingTab()
    {
        InitializeComponent();
    }

    public void Initialize(
        Window owner,
        OptionsSubtitleZonesTab zones,
        IMediaSessionRegistry mediaSessionRegistry,
        IWindowFactory windowFactory,
        IFilePickerService filePicker,
        ISubtitleRouteApplier routeApplier,
        ILoggerFactory loggerFactory)
    {
        _owner = owner;
        _zones = zones;
        _mediaSessionRegistry = mediaSessionRegistry;
        _windowFactory = windowFactory;
        _filePicker = filePicker;
        _routeApplier = routeApplier;
        _logger = loggerFactory.CreateLogger<OptionsSubtitleRoutingTab>();

        var session = _mediaSessionRegistry.Current;
        _streamOptions.AddRange((session?.SubtitleStreams ?? []).Select(s => new SubtitleStreamOption(s)));

        foreach (var route in session?.SubtitleRoutes ?? [])
        {
            var zone = _zones.Zones.FirstOrDefault(z => z.Id == route.ZoneId);
            if (zone is not null)
            {
                var option = _streamOptions.FirstOrDefault(o => o.Stream.Id == route.Stream.Id) ??
                             new SubtitleStreamOption(route.Stream);
                _rows.Add(new SubtitleRouteRow(zone, option));
            }
        }

        SubtitleRoutesList.ItemsSource = _rows;
        _zones.ZonesChanged += OnZonesChanged;

        UpdateStreamOptions();
        UpdateZoneSelector();
    }

    public void Dispose() => _zones.ZonesChanged -= OnZonesChanged;

    private void OnRowTrackPicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not SubtitleTrackPicker { DataContext: SubtitleRouteRow row } picker)
        {
            return;
        }

        row.SelectedStreamOption = picker.SelectedOption;
        ApplySubtitleRoutes();
    }

    private void OnDeleteSubtitleRoute(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleRouteRow row)
        {
            return;
        }

        _rows.Remove(row);
        UpdateZoneSelector();
        ApplySubtitleRoutes();
    }

    private void OnZonesChanged()
    {
        UpdateZoneSelector();
        RepointRowsAtCurrentZones();

        var orphanedRows = _rows.Where(row => _zones.Zones.All(z => z.Id != row.Zone.Id)).ToList();
        if (orphanedRows.Count == 0)
        {
            return;
        }

        foreach (var row in orphanedRows)
        {
            _rows.Remove(row);
        }

        UpdateZoneSelector();
        ApplySubtitleRoutes();
    }

    private void RepointRowsAtCurrentZones()
    {
        foreach (var row in _rows)
        {
            if (_zones.Zones.FirstOrDefault(z => z.Id == row.Zone.Id) is { } current)
            {
                row.Zone = current;
            }
        }
    }

    private void OnDraftSubtitleZoneChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SubtitleZoneSelector.SelectedItem is not SubtitleZone)
        {
            DraftTrackPicker.IsEnabled = false;
            DraftTrackPicker.SelectedOption = null;
            return;
        }

        DraftTrackPicker.IsEnabled = true;
        var supported = _streamOptions.Where(o => o.IsSupported).ToList();
        if (supported.Count == 1)
        {
            DraftTrackPicker.SelectedOption = supported[0];
        }

        TryCommitDraftSubtitleRoute();
    }

    private void OnDraftTrackPicked(object? sender, RoutedEventArgs e) => TryCommitDraftSubtitleRoute();

    private void OnClearDraftSubtitleRoute(object? sender, RoutedEventArgs e) =>
        SubtitleZoneSelector.SelectedItem = null;

    private void TryCommitDraftSubtitleRoute()
    {
        if (SubtitleZoneSelector.SelectedItem is not SubtitleZone zone ||
            DraftTrackPicker.SelectedOption is not { IsSupported: true } streamOption)
        {
            return;
        }

        _rows.Add(new SubtitleRouteRow(zone, streamOption) { AvailableStreamOptions = _streamOptions.ToList() });
        ApplySubtitleRoutes();

        DraftTrackPicker.SelectedOption = null;
        UpdateZoneSelector();
        SubtitleZoneSelector.Focus();
    }

    private async void OnLoadSubtitleFile(object? sender, RoutedEventArgs e)
    {
        var session = _mediaSessionRegistry.Current;
        if (session is null)
        {
            return;
        }

        var path = await _filePicker.PickFileAsync(_owner, Strings.Options_LoadSubtitleFileTitle, _subtitleFileTypeFilter);
        if (path is null)
        {
            return;
        }

        try
        {
            var sidecar = new SubtitleSidecarSource(path, Title: Path.GetFileNameWithoutExtension(path));
            var added = await Task.Run(() => session.AddSubtitleSidecar(sidecar));

            _streamOptions.Add(new SubtitleStreamOption(added));
            UpdateStreamOptions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load subtitle file {Path}.", path);

            await _windowFactory.ShowDialogAsync<OpenFileErrorWindow>(
                _owner, w => w.Load(Strings.OpenFileError_SubtitleHeading, ex.Message));
        }
    }

    private async void ApplySubtitleRoutes()
    {
        var session = _mediaSessionRegistry.Current;
        if (session is null)
        {
            return;
        }

        SubtitleRouteStatusText.IsVisible = false;
        var routes = _rows.Select(row => new SubtitleRoute(row.Stream, row.Zone.Id)).ToList();

        // The retry notice is posted from a worker thread and can land after this apply has
        // already finished (or been superseded) - it must not overwrite that newer status.
        var applyId = ++_applyCount;
        var finished = false;

        try
        {
            var applied = await _routeApplier.ApplyAsync(
                session,
                routes,
                () => Dispatcher.UIThread.Post(() =>
                {
                    if (!finished && applyId == _applyCount)
                    {
                        ShowRetryingStatus();
                    }
                }));
            finished = true;

            if (applied is not null && applyId == _applyCount)
            {
                ReconcileSubtitleRoutes(routes, applied);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Applying subtitle routes failed.");
        }
    }

    private void ReconcileSubtitleRoutes(IReadOnlyList<SubtitleRoute> requested, IReadOnlyList<SubtitleRoute> applied)
    {
        var failedRows = _rows
            .Where(row => requested.Any(r => r.Stream.Id == row.Stream.Id && r.ZoneId == row.Zone.Id))
            .Where(row => !applied.Any(r => r.Stream.Id == row.Stream.Id && r.ZoneId == row.Zone.Id))
            .ToList();

        if (failedRows.Count == 0)
        {
            SubtitleRouteStatusText.IsVisible = false;
            return;
        }

        foreach (var row in failedRows)
        {
            _rows.Remove(row);
        }

        UpdateZoneSelector();
        ShowStatus(Strings.Options_SubtitleRouteError, Brushes.IndianRed);
    }

    private void ShowRetryingStatus() =>
        ShowStatus(Strings.Options_SubtitleRouteRetrying, this.FindResource("SettingsHintBrush") as IBrush);

    private void ShowStatus(string text, IBrush? foreground)
    {
        SubtitleRouteStatusText.Text = text;
        SubtitleRouteStatusText.Foreground = foreground;
        SubtitleRouteStatusText.IsVisible = true;
    }

    private void UpdateStreamOptions()
    {
        var options = _streamOptions.ToList();
        DraftTrackPicker.Options = options;

        foreach (var row in _rows)
        {
            row.AvailableStreamOptions = options;
        }
    }

    private void UpdateZoneSelector() =>
        OptionsSelector.Rebind(
            SubtitleZoneSelector, _zones.Zones, _rows.Select(row => row.Zone.Id), z => z.Id);
}
