using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OMP.Lib.Subtitle;
using OMP.Ui.Controls;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;
using OMP.Ui.Tests.TestDoubles;
using OMP.Ui.Windows;

namespace OMP.Ui.Tests.Controls;

/// <summary>
/// Covers the two subtitle tabs together, because the interesting behaviour is the seam between
/// them: zone CRUD raises an event and routing reacts. Every control's event, including the ones
/// inside each <c>ItemsControl</c>'s <c>DataTemplate</c>, is declared directly in each tab's own
/// XAML, so a dropped subscription fails the build - what remains here is verifying each handler
/// does the right thing, driving the real realized controls rather than calling handlers directly.
/// </summary>
public class OptionsSubtitleTabsTests
{
    private static readonly SubtitleStream _english = new(1, "subrip", "English", "en", IsTextBased: true);
    private static readonly SubtitleStream _french = new(2, "subrip", "French", "fr", IsTextBased: true);

    [AvaloniaFact]
    public void ZonesTab_SeedsFromSettingsAndBindsTheList()
    {
        var h = new Harness();

        Assert.Equal(3, h.Zones.Zones.Count);
        Assert.Same(h.Zones.Zones, h.Zones.ZonesList.ItemsSource);
    }

    [AvaloniaFact]
    public void ZonesTab_ClonesZonesSoEditsDoNotLeakIntoSettingsUntilPersisted()
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

        h.FindZoneRowControl<Button>(zone, "DeleteZoneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, raised);
        Assert.DoesNotContain(h.Settings.SubtitleZones, z => z.Id == zone.Id);
    }

    [AvaloniaFact]
    public void DeletingABuiltInZone_IsRefused()
    {
        var h = new Harness();
        var builtIn = h.Zones.Zones.First(z => z.IsBuiltIn);

        h.FindZoneRowControl<Button>(builtIn, "DeleteZoneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Contains(h.Zones.Zones, z => z.Id == builtIn.Id);
    }

    [AvaloniaFact]
    public void SelectingATrackAndZone_CommitsASubtitleRoute()
    {
        var h = new Harness();

        h.Routing.SubtitleStreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.Routing.SubtitleZoneSelector.SelectedItem = h.Zones.Zones[0];
        h.Session.WaitForSubtitleRoutes(1);

        var applied = Assert.Single(h.Session.AppliedSubtitleRoutes);
        var route = Assert.Single(applied);
        Assert.Equal(_english.Id, route.Stream.Id);
    }

    [AvaloniaFact]
    public void ZoneSelector_IsDisabledUntilATrackIsChosen()
    {
        var h = new Harness();

        h.Routing.SubtitleStreamSelector.SelectedItem = h.StreamOptionFor(_english);
        Assert.True(h.Routing.SubtitleZoneSelector.IsEnabled);

        h.Routing.SubtitleStreamSelector.SelectedItem = null;
        Assert.False(h.Routing.SubtitleZoneSelector.IsEnabled);
    }

    [AvaloniaFact]
    public void ClearDraftButton_ClearsTheTrackSelection()
    {
        var h = new Harness();
        h.Routing.SubtitleStreamSelector.SelectedItem = h.StreamOptionFor(_english);

        h.Routing.ClearDraftSubtitleRouteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(h.Routing.SubtitleStreamSelector.SelectedItem);
    }

    /// <summary>
    /// The cross-tab path, and the one most likely to break: deleting a zone that a route points at
    /// has to drop that route and reapply the rest.
    /// </summary>
    [AvaloniaFact]
    public void DeletingARoutedZone_DropsTheOrphanedRouteAndReapplies()
    {
        var h = new Harness();
        var zone = h.Zones.Zones.First(z => !z.IsBuiltIn);

        h.Routing.SubtitleStreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.Routing.SubtitleZoneSelector.SelectedItem = zone;
        h.Session.WaitForSubtitleRoutes(1);
        Assert.Single(h.Rows);

        h.FindZoneRowControl<Button>(zone, "DeleteZoneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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

        h.Routing.SubtitleStreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.Routing.SubtitleZoneSelector.SelectedItem = routedZone;
        h.Session.WaitForSubtitleRoutes(1);
        var appliedBefore = h.Session.AppliedSubtitleRoutes.Count;

        h.FindZoneRowControl<Button>(otherZone, "DeleteZoneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        h.Session.SettleRouteApplications();

        Assert.Single(h.Rows);
        Assert.Equal(appliedBefore, h.Session.AppliedSubtitleRoutes.Count);
    }

    [AvaloniaFact]
    public void RenamingARoutedZone_UpdatesTheRowLabel()
    {
        var h = new Harness();
        var zone = h.Zones.Zones.First(z => z.Id == "custom-zone");

        h.Routing.SubtitleStreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.Routing.SubtitleZoneSelector.SelectedItem = zone;
        h.Session.WaitForSubtitleRoutes(1);
        Assert.Equal("Custom", h.Rows.Single().ZoneLabel);

        var renamed = new SubtitleZone { Id = zone.Id, Name = "Renamed" };
        h.Zones.Zones[h.Zones.Zones.IndexOf(zone)] = renamed;
        Dispatcher.UIThread.RunJobs();

        var otherZone = h.Zones.Zones.First(z => z.Id == "other-zone");
        h.FindZoneRowControl<Button>(otherZone, "DeleteZoneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
            h.Routing.SubtitleStreamSelector.ItemsSource!.Cast<SubtitleStreamOption>(),
            o => o.Stream.Title == "external");
    }

    [AvaloniaFact]
    public void LoadSubtitleFileButton_CancelledPicker_AddsNoStreamOption()
    {
        var h = new Harness();
        var optionsBefore = h.Routing.SubtitleStreamSelector.ItemsSource!.Cast<SubtitleStreamOption>().Count();
        h.FilePicker
            .Setup(p => p.PickFileAsync(h.Window, It.IsAny<string>(), It.IsAny<FilePickerFileType>()))
            .ReturnsAsync((string?)null);

        h.RaiseLoadSubtitleFileClick();

        Assert.Equal(optionsBefore, h.Routing.SubtitleStreamSelector.ItemsSource!.Cast<SubtitleStreamOption>().Count());
    }

    [AvaloniaFact]
    public void Dispose_StopsReactingToZoneChanges()
    {
        var h = new Harness();
        var zone = h.Zones.Zones.First(z => !z.IsBuiltIn);

        h.Routing.SubtitleStreamSelector.SelectedItem = h.StreamOptionFor(_english);
        h.Routing.SubtitleZoneSelector.SelectedItem = zone;

        h.Session.WaitForSubtitleRoutes(1);
        h.Routing.Dispose();
        h.FindZoneRowControl<Button>(zone, "DeleteZoneButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        h.Session.SettleRouteApplications();

        Assert.Single(h.Rows);
    }

    private sealed class Harness
    {
        public OptionsSubtitleZonesTab Zones { get; } = new();

        public OptionsSubtitleRoutingTab Routing { get; } = new();

        public Window Window { get; }

        public FakeMediaSession Session { get; }

        public UserSettings Settings { get; } = new();

        public Mock<IWindowFactory> WindowFactory { get; } = new();

        public Mock<IFilePickerService> FilePicker { get; } = new();

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

            var panel = new StackPanel { Children = { Zones, Routing } };
            Window = new Window { Content = panel };
            Window.Show();

            Zones.Initialize(Window, WindowFactory.Object, settingsService.Object);
            Routing.Initialize(Window, Zones, registry, WindowFactory.Object, FilePicker.Object, NullLoggerFactory.Instance);
            Dispatcher.UIThread.RunJobs();
        }

        public IEnumerable<SubtitleRouteRow> Rows => Routing.SubtitleRoutesList.ItemsSource!.Cast<SubtitleRouteRow>();

        public SubtitleStreamOption StreamOptionFor(SubtitleStream stream) =>
            Routing.SubtitleStreamSelector.ItemsSource!.Cast<SubtitleStreamOption>().First(o => o.Stream.Id == stream.Id);

        public T FindZoneRowControl<T>(SubtitleZone zone, string name) where T : Control =>
            Zones.ZonesList.GetVisualDescendants().OfType<T>().First(c => c.Name == name && Equals(c.DataContext, zone));

        public void RaiseAddZoneClick()
        {
            Zones.AddZoneButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
        }

        public void RaiseLoadSubtitleFileClick()
        {
            Routing.LoadSubtitleFileButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            for (var i = 0; i < 40; i++)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(5);
            }
        }
    }
}
