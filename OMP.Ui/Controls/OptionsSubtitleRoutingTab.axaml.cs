using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
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
    private ILogger _logger = null!;

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
        ILoggerFactory loggerFactory)
    {
        _owner = owner;
        _zones = zones;
        _mediaSessionRegistry = mediaSessionRegistry;
        _windowFactory = windowFactory;
        _filePicker = filePicker;
        _logger = loggerFactory.CreateLogger<OptionsSubtitleRoutingTab>();

        var session = _mediaSessionRegistry.Current;
        _streamOptions.AddRange((session?.SubtitleStreams ?? []).Select(s => new SubtitleStreamOption(s)));

        foreach (var route in session?.SubtitleRoutes ?? [])
        {
            var zone = _zones.Zones.FirstOrDefault(z => z.Id == route.ZoneId);
            if (zone is not null)
            {
                _rows.Add(new SubtitleRouteRow(route.Stream, zone));
            }
        }

        SubtitleRoutesList.ItemsSource = _rows;
        _zones.ZonesChanged += OnZonesChanged;

        UpdateStreamSelector();
        UpdateZoneSelector();
    }

    public void Dispose() => _zones.ZonesChanged -= OnZonesChanged;

    private void OnDeleteSubtitleRoute(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not SubtitleRouteRow row)
        {
            return;
        }

        _rows.Remove(row);
        UpdateStreamSelector();
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

        UpdateStreamSelector();
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
        _rows.Add(new SubtitleRouteRow(streamOption.Stream, zone));
        ApplySubtitleRoutes();

        UpdateStreamSelector();
        UpdateZoneSelector();
        SubtitleStreamSelector.Focus();
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
            UpdateStreamSelector();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load subtitle file {Path}.", path);

            await _windowFactory.ShowDialogAsync<OpenFileErrorWindow>(
                _owner, w => w.Load(Strings.OpenFileError_SubtitleHeading, ex.Message));
        }
    }

    private void ApplySubtitleRoutes()
    {
        var session = _mediaSessionRegistry.Current;
        if (session is null)
        {
            return;
        }

        var routes = _rows.Select(row => new SubtitleRoute(row.Stream, row.Zone.Id)).ToList();
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
        var failedRows = _rows
            .Where(row => !applied.Any(r => r.Stream.Id == row.Stream.Id && r.ZoneId == row.Zone.Id))
            .ToList();

        if (failedRows.Count == 0)
        {
            return;
        }

        foreach (var row in failedRows)
        {
            _rows.Remove(row);
        }

        UpdateStreamSelector();
        UpdateZoneSelector();

        SubtitleRouteErrorText.Text = Strings.Options_SubtitleRouteError;
        SubtitleRouteErrorText.IsVisible = true;
    }

    private void UpdateStreamSelector() =>
        OptionsSelector.Rebind(
            SubtitleStreamSelector, _streamOptions, _rows.Select(row => row.Stream.Id), o => o.Stream.Id);

    private void UpdateZoneSelector() =>
        OptionsSelector.Rebind(
            SubtitleZoneSelector, _zones.Zones, _rows.Select(row => row.Zone.Id), z => z.Id);
}
