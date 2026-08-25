using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OMP.Lib.Subtitle;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;
using OMP.Ui.Tests.TestDoubles;
using OMP.Ui.Windows;

namespace OMP.Ui.Tests.Services;

/// <summary>
/// Covers the two subtitle sections together, because the interesting behaviour is the seam
/// between them: zone CRUD raises an event and routing reacts. Both halves used to live in one
/// window method, so nothing could have gone silently unwired.
/// </summary>
public class OptionsSubtitleSectionsTests
{
    private static readonly SubtitleStream _english = new(1, "subrip", "English", "en", IsTextBased: true);
    private static readonly SubtitleStream _french = new(2, "subrip", "French", "fr", IsTextBased: true);

    [AvaloniaFact]
    public void ZonesSection_SeedsFromSettingsAndBindsTheList()
    {
        var h = new Harness();

        Assert.Equal(3, h.Zones.Zones.Count);
        Assert.Same(h.Zones.Zones, h.ZonesList.ItemsSource);
    }

    [AvaloniaFact]
    public void ZonesSection_ClonesZonesSoEditsDoNotLeakIntoSettingsUntilPersisted()
    {
        var h = new Harness();

        Assert.DoesNotContain(h.Zones.Zones, z => ReferenceEquals(z, h.Settings.SubtitleZones[0]));
    }

    [AvaloniaFact]
    public void DeletingAZone_PersistsAndRaisesZonesChanged()
    {
        var h = new Harness();
        var raised = 0;
        h.Zones.ZonesChanged += () => raised++;
        var zone = h.Zones.Zones.First(z => !z.IsBuiltIn);

        h.Zones.OnDeleteZone(new Button { DataContext = zone }, new RoutedEventArgs());

        Assert.Equal(1, raised);
        Assert.DoesNotContain(h.Settings.SubtitleZones, z => z.Id == zone.Id);
    }

    [AvaloniaFact]
    public void DeletingABuiltInZone_IsRefused()
    {
        var h = new Harness();
        var builtIn = h.Zones.Zones.First(z => z.IsBuiltIn);

        h.Zones.OnDeleteZone(new Button { DataContext = builtIn }, new RoutedEventArgs());

        Assert.Contains(h.Zones.Zones, z => z.Id == builtIn.Id);
    }

    [AvaloniaFact]
    public void SelectingATrackAndZone_CommitsASubtitleRoute()
    {
        var h = new Harness();

        h.StreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.ZoneSelector.SelectedItem = h.Zones.Zones[0];
        h.Session.WaitForSubtitleRoutes(1);

        var applied = Assert.Single(h.Session.AppliedSubtitleRoutes);
        var route = Assert.Single(applied);
        Assert.Equal(_english.Id, route.Stream.Id);
    }

    [AvaloniaFact]
    public void ZoneSelector_IsDisabledUntilATrackIsChosen()
    {
        var h = new Harness();

        h.StreamSelector.SelectedItem = h.StreamOptionFor(_english);
        Assert.True(h.ZoneSelector.IsEnabled);

        h.StreamSelector.SelectedItem = null;
        Assert.False(h.ZoneSelector.IsEnabled);
    }

    [AvaloniaFact]
    public void ClearDraftButton_ClearsTheTrackSelection()
    {
        var h = new Harness();
        h.StreamSelector.SelectedItem = h.StreamOptionFor(_english);

        h.ClearDraftButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(h.StreamSelector.SelectedItem);
    }

    /// <summary>
    /// The cross-section path, and the one most likely to break: deleting a zone that a route
    /// points at has to drop that route and reapply the rest.
    /// </summary>
    [AvaloniaFact]
    public void DeletingARoutedZone_DropsTheOrphanedRouteAndReapplies()
    {
        var h = new Harness();
        var zone = h.Zones.Zones.First(z => !z.IsBuiltIn);

        h.StreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.ZoneSelector.SelectedItem = zone;
        h.Session.WaitForSubtitleRoutes(1);
        Assert.Single(h.Rows);

        h.Zones.OnDeleteZone(new Button { DataContext = zone }, new RoutedEventArgs());
        h.Session.WaitForSubtitleRoutes(2);

        Assert.Empty(h.Rows);
        Assert.Empty(h.Session.AppliedSubtitleRoutes.Last());
    }

    [AvaloniaFact]
    public void DeletingAnUnroutedZone_LeavesRoutesAlone()
    {
        var h = new Harness();
        var routedZone = h.Zones.Zones.First(z => z.Id == "custom-zone");
        var otherZone = h.Zones.Zones.First(z => z.Id == "other-zone");

        h.StreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.ZoneSelector.SelectedItem = routedZone;
        h.Session.WaitForSubtitleRoutes(1);
        var appliedBefore = h.Session.AppliedSubtitleRoutes.Count;

        h.Zones.OnDeleteZone(new Button { DataContext = otherZone }, new RoutedEventArgs());
        h.Session.SettleRouteApplications();

        Assert.Single(h.Rows);
        Assert.Equal(appliedBefore, h.Session.AppliedSubtitleRoutes.Count);
    }

    [AvaloniaFact]
    public void RenamingARoutedZone_UpdatesTheRowLabel()
    {
        var h = new Harness();
        var zone = h.Zones.Zones.First(z => z.Id == "custom-zone");

        h.StreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.ZoneSelector.SelectedItem = zone;
        h.Session.WaitForSubtitleRoutes(1);
        Assert.Equal("Custom", h.Rows.Single().ZoneLabel);

        var renamed = new SubtitleZone { Id = zone.Id, Name = "Renamed" };
        h.Zones.Zones[h.Zones.Zones.IndexOf(zone)] = renamed;

        h.Zones.OnDeleteZone(
            new Button { DataContext = h.Zones.Zones.First(z => z.Id == "other-zone") }, new RoutedEventArgs());

        var row = h.Rows.Single();
        Assert.Same(renamed, row.Zone);
        Assert.Equal("Renamed", row.ZoneLabel);
    }

    [AvaloniaFact]
    public void AddZoneButton_AddsWhateverTheEditorReturns()
    {
        var h = new Harness();
        var raised = 0;
        h.Zones.ZonesChanged += () => raised++;
        var newZone = new SubtitleZone { Id = "new-zone", Name = "New" };
        h.WindowFactory
            .Setup(f => f.ShowDialogAsync<SubtitleZoneEditorWindow, SubtitleZone>(
                h.Window, It.IsAny<Action<SubtitleZoneEditorWindow>>()))
            .ReturnsAsync(newZone);

        h.RaiseAddZoneClick();

        Assert.Contains(h.Zones.Zones, z => z.Id == "new-zone");
        Assert.Contains(h.Settings.SubtitleZones, z => z.Id == "new-zone");
        Assert.Equal(1, raised);
    }

    [AvaloniaFact]
    public void AddZoneButton_CancelledEditor_AddsNothing()
    {
        var h = new Harness();
        var countBefore = h.Zones.Zones.Count;
        h.WindowFactory
            .Setup(f => f.ShowDialogAsync<SubtitleZoneEditorWindow, SubtitleZone>(
                h.Window, It.IsAny<Action<SubtitleZoneEditorWindow>>()))
            .ReturnsAsync((SubtitleZone?)null);

        h.RaiseAddZoneClick();

        Assert.Equal(countBefore, h.Zones.Zones.Count);
    }

    [AvaloniaFact]
    public void LoadSubtitleFileButton_AddsTheSidecarAsAStreamOption()
    {
        var h = new Harness();
        h.FilePicker
            .Setup(p => p.PickFileAsync(h.Window, It.IsAny<string>(), It.IsAny<FilePickerFileType>()))
            .ReturnsAsync(@"C:\subs\external.srt");

        h.RaiseLoadSubtitleFileClick();

        Assert.Contains(
            h.StreamSelector.ItemsSource!.Cast<SubtitleStreamOption>(),
            o => o.Stream.Title == "external");
    }

    [AvaloniaFact]
    public void LoadSubtitleFileButton_CancelledPicker_AddsNoStreamOption()
    {
        var h = new Harness();
        var optionsBefore = h.StreamSelector.ItemsSource!.Cast<SubtitleStreamOption>().Count();
        h.FilePicker
            .Setup(p => p.PickFileAsync(h.Window, It.IsAny<string>(), It.IsAny<FilePickerFileType>()))
            .ReturnsAsync((string?)null);

        h.RaiseLoadSubtitleFileClick();

        Assert.Equal(optionsBefore, h.StreamSelector.ItemsSource!.Cast<SubtitleStreamOption>().Count());
    }

    [AvaloniaFact]
    public void Dispose_StopsReactingToZoneChanges()
    {
        var h = new Harness();
        var zone = h.Zones.Zones.First(z => !z.IsBuiltIn);

        h.StreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.ZoneSelector.SelectedItem = zone;

        h.Session.WaitForSubtitleRoutes(1);
        h.Routing.Dispose();
        h.Zones.OnDeleteZone(new Button { DataContext = zone }, new RoutedEventArgs());
        h.Session.SettleRouteApplications();

        Assert.Single(h.Rows);
    }

    private sealed class Harness
    {
        public ItemsControl ZonesList { get; } = new();

        public ItemsControl RoutesList { get; } = new();

        public ComboBox StreamSelector { get; } = new();

        public ComboBox ZoneSelector { get; } = new();

        public Button AddZoneButton { get; } = new();

        public Button ClearDraftButton { get; } = new();

        public Button LoadFileButton { get; } = new();

        public TextBlock ErrorText { get; } = new();

        public FakeMediaSession Session { get; }

        public UserSettings Settings { get; } = new();

        public OptionsSubtitleZonesSection Zones { get; }

        public OptionsSubtitleRoutingSection Routing { get; }

        public Mock<IWindowFactory> WindowFactory { get; } = new();

        public Mock<IFilePickerService> FilePicker { get; } = new();

        public Window Window { get; } = new();

        public Harness()
        {
            Settings.SubtitleZones =
            [
                SubtitleZone.CreateBuiltIns()[0],
                new SubtitleZone { Id = "custom-zone", Name = "Custom" },
                new SubtitleZone { Id = "other-zone", Name = "Other" }
            ];

            Session = new FakeMediaSession { SubtitleStreams = [_english, _french] };

            var settingsService = new Mock<IUserSettingsService>();
            settingsService.Setup(s => s.Current).Returns(Settings);
            settingsService.Setup(s => s.Save());

            var registry = new FakeMediaSessionRegistry { Current = Session };

            Zones = new OptionsSubtitleZonesSection(
                Window, ZonesList, AddZoneButton, WindowFactory.Object, settingsService.Object);

            Routing = new OptionsSubtitleRoutingSection(
                Window,
                RoutesList,
                StreamSelector,
                ZoneSelector,
                ClearDraftButton,
                LoadFileButton,
                ErrorText,
                Zones,
                registry,
                WindowFactory.Object,
                FilePicker.Object,
                NullLoggerFactory.Instance);
        }

        public void RaiseAddZoneClick()
        {
            AddZoneButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        public void RaiseLoadSubtitleFileClick()
        {
            LoadFileButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            for (var i = 0; i < 40; i++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
        }

        public IEnumerable<SubtitleRouteRow> Rows => RoutesList.ItemsSource!.Cast<SubtitleRouteRow>();

        public SubtitleStreamOption StreamOptionFor(SubtitleStream stream) =>
            StreamSelector.ItemsSource!.Cast<SubtitleStreamOption>().First(o => o.Stream.Id == stream.Id);
    }
}
