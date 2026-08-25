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
using OMP.Ui.Settings;
using OMP.Ui.Windows;

namespace OMP.Ui.Services;

/// <summary>
/// The Subtitles tab of the Options window: routing a subtitle track to a zone, and attaching an
/// external subtitle file. Binds against the zone collection owned by
/// <see cref="OptionsSubtitleZonesSection"/> and prunes its own rows when a zone disappears, so
/// zone CRUD never has to know that routing exists.
/// </summary>
internal sealed class OptionsSubtitleRoutingSection : IDisposable
{
    private static readonly FilePickerFileType _subtitleFileTypeFilter = new(Strings.Options_SubtitleFileTypeFilterName)
    {
        Patterns = ["*.srt", "*.vtt", "*.ass", "*.ssa", "*.sub"]
    };

    private readonly Window _owner;
    private readonly ComboBox _streamSelector;
    private readonly ComboBox _zoneSelector;
    private readonly TextBlock _errorText;
    private readonly OptionsSubtitleZonesSection _zones;
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IWindowFactory _windowFactory;
    private readonly IFilePickerService _filePicker;
    private readonly ILogger _logger;
    private readonly ObservableCollection<SubtitleRouteRow> _rows = [];
    private readonly List<SubtitleStreamOption> _streamOptions = [];

    public OptionsSubtitleRoutingSection(
        Window owner,
        ItemsControl routesList,
        ComboBox streamSelector,
        ComboBox zoneSelector,
        Button clearDraftButton,
        Button loadSubtitleFileButton,
        TextBlock errorText,
        OptionsSubtitleZonesSection zones,
        IMediaSessionRegistry mediaSessionRegistry,
        IWindowFactory windowFactory,
        IFilePickerService filePicker,
        ILoggerFactory loggerFactory)
    {
        _owner = owner;
        _streamSelector = streamSelector;
        _zoneSelector = zoneSelector;
        _errorText = errorText;
        _zones = zones;
        _mediaSessionRegistry = mediaSessionRegistry;
        _windowFactory = windowFactory;
        _filePicker = filePicker;
        _logger = loggerFactory.CreateLogger<OptionsSubtitleRoutingSection>();

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

        routesList.ItemsSource = _rows;
        clearDraftButton.Click += OnClearDraftSubtitleRoute;
        loadSubtitleFileButton.Click += OnLoadSubtitleFile;
        _streamSelector.SelectionChanged += OnDraftSubtitleStreamChanged;
        _zoneSelector.SelectionChanged += OnDraftSubtitleZoneChanged;
        _zones.ZonesChanged += OnZonesChanged;

        UpdateStreamSelector();
        UpdateZoneSelector();
    }

    public void Dispose() => _zones.ZonesChanged -= OnZonesChanged;

    internal void OnDeleteSubtitleRoute(object? sender, RoutedEventArgs e)
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
        if (_streamSelector.SelectedItem is not SubtitleStreamOption { IsSupported: true })
        {
            _zoneSelector.IsEnabled = false;
            _zoneSelector.SelectedItem = null;
            return;
        }

        _zoneSelector.IsEnabled = true;
        if (_zoneSelector.Items.Count == 1)
        {
            _zoneSelector.SelectedIndex = 0;
        }

        TryCommitDraftSubtitleRoute();
    }

    private void OnDraftSubtitleZoneChanged(object? sender, SelectionChangedEventArgs e) =>
        TryCommitDraftSubtitleRoute();

    private void OnClearDraftSubtitleRoute(object? sender, RoutedEventArgs e) =>
        _streamSelector.SelectedItem = null;

    private void TryCommitDraftSubtitleRoute()
    {
        if (_streamSelector.SelectedItem is not SubtitleStreamOption { IsSupported: true } streamOption ||
            _zoneSelector.SelectedItem is not SubtitleZone zone)
        {
            return;
        }

        _errorText.IsVisible = false;
        _rows.Add(new SubtitleRouteRow(streamOption.Stream, zone));
        ApplySubtitleRoutes();

        UpdateStreamSelector();
        UpdateZoneSelector();
        _streamSelector.Focus();
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

        _errorText.Text = Strings.Options_SubtitleRouteError;
        _errorText.IsVisible = true;
    }

    private void UpdateStreamSelector() =>
        OptionsSelector.Rebind(
            _streamSelector, _streamOptions, _rows.Select(row => row.Stream.Id), o => o.Stream.Id);

    private void UpdateZoneSelector() =>
        OptionsSelector.Rebind(
            _zoneSelector, _zones.Zones, _rows.Select(row => row.Zone.Id), z => z.Id);
}
