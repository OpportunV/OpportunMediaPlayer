using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Ui.Controls;
using OMP.Ui.Models;
using OMP.Ui.Settings;
using OMP.Ui.Tests.TestDoubles;

namespace OMP.Ui.Tests.Controls;

/// <summary>
/// Behaviour coverage for the Audio Routing tab. Every control's event, including the ones inside
/// the per-row <c>DataTemplate</c>, is declared directly in <c>OptionsAudioRoutingTab.axaml</c>, so
/// a dropped subscription fails the build - what remains here is verifying each handler does the
/// right thing, driving the real realized controls rather than calling handlers directly.
/// </summary>
public class OptionsAudioRoutingTabTests
{
    private static readonly AudioStream _mainStream = new(1, "aac", "Main", "en");
    private static readonly AudioStream _commentaryStream = new(2, "aac", "Commentary", "en");
    private static readonly AudioOutput _speakers = new(1, "Speakers");
    private static readonly AudioOutput _headset = new(2, "Headset");

    [AvaloniaFact]
    public void SelectingAnOutputAndTrack_CommitsARouteToTheSession()
    {
        var h = new Harness();

        h.Tab.OutputSelector.SelectedItem = _speakers;
        h.Tab.StreamSelector.SelectedItem = h.StreamOptionFor(_mainStream);
        h.Session.WaitForAudioRoutes(1);

        var applied = Assert.Single(h.Session.AppliedAudioRoutes);
        var route = Assert.Single(applied);
        Assert.Equal(_mainStream.Id, route.Stream.Id);
        Assert.Equal(_speakers.Id, route.Output.Id);
    }

    [AvaloniaFact]
    public void ClearDraftButton_ClearsTheOutputSelection()
    {
        var h = new Harness();
        h.Tab.OutputSelector.SelectedItem = _speakers;

        h.Tab.ClearDraftRouteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(h.Tab.OutputSelector.SelectedItem);
    }

    [AvaloniaFact]
    public void TrackPicker_IsDisabledUntilAnOutputIsChosen_AndDisabledAgainWhenCleared()
    {
        var h = new Harness();

        h.Tab.OutputSelector.SelectedItem = _speakers;
        Assert.True(h.Tab.StreamSelector.IsEnabled);

        h.Tab.OutputSelector.SelectedItem = null;
        Assert.False(h.Tab.StreamSelector.IsEnabled);
        Assert.Null(h.Tab.StreamSelector.SelectedItem);
    }

    [AvaloniaFact]
    public void CommittingARoute_RemovesThatOutputFromTheDraftPicker()
    {
        var h = new Harness();

        h.Tab.OutputSelector.SelectedItem = _speakers;
        h.Tab.StreamSelector.SelectedItem = h.StreamOptionFor(_mainStream);

        var remaining = h.Tab.OutputSelector.ItemsSource!.Cast<AudioOutput>().ToList();
        Assert.DoesNotContain(remaining, o => o.Id == _speakers.Id);
        Assert.Contains(remaining, o => o.Id == _headset.Id);
    }

    [AvaloniaFact]
    public void TheSameTrackMayFeedTwoOutputs()
    {
        var h = new Harness();

        h.Tab.OutputSelector.SelectedItem = _speakers;
        h.Tab.StreamSelector.SelectedItem = h.StreamOptionFor(_mainStream);
        h.Tab.OutputSelector.SelectedItem = _headset;
        h.Tab.StreamSelector.SelectedItem = h.StreamOptionFor(_mainStream);
        h.Session.WaitForAudioRoutes(2);

        var applied = h.Session.AppliedAudioRoutes.Last();
        Assert.Equal(2, applied.Count);
        Assert.All(applied, r => Assert.Equal(_mainStream.Id, r.Stream.Id));
    }

    [AvaloniaFact]
    public void OnRouteVolumeChanged_PushesToTheSessionAndPersists()
    {
        var h = new Harness(withExistingRoute: true);

        var slider = h.FindRowControl<Slider>("RouteVolumeSlider");
        slider.Value = 40;

        Assert.Contains(h.Session.OutputVolumeCalls, c => c.OutputId == _speakers.Id && Math.Abs(c.Volume - 0.4) < .001);
        Assert.Contains(h.Settings.OutputVolumes, o => o.FriendlyName == "Speakers");
    }

    [AvaloniaFact]
    public void DeleteRoute_IsRefusedWhileOnlyOneRouteRemains()
    {
        var h = new Harness(withExistingRoute: true);

        var deleteButton = h.FindRowControl<Button>("DeleteRouteButton");
        deleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        h.Session.SettleRouteApplications();

        Assert.Single(h.Rows);
        Assert.Empty(h.Session.AppliedAudioRoutes);
    }

    private sealed class Harness
    {
        public OptionsAudioRoutingTab Tab { get; }

        public Window Window { get; }

        public FakeMediaSession Session { get; }

        public UserSettings Settings { get; } = new();

        public Harness(bool withExistingRoute = false)
        {
            Session = new FakeMediaSession
            {
                AudioStreams = [_mainStream, _commentaryStream],
                AudioOutputs = [_speakers, _headset],
                AudioRoutes = withExistingRoute ? [new AudioRoute(_mainStream, _speakers)] : []
            };

            var settingsService = new Mock<IUserSettingsService>();
            settingsService.Setup(s => s.Current).Returns(Settings);

            Tab = new OptionsAudioRoutingTab();
            Window = new Window { Content = Tab };
            Window.Show();

            Tab.Initialize(
                new FakeMediaSessionRegistry { Current = Session }, settingsService.Object, NullLoggerFactory.Instance);
            Dispatcher.UIThread.RunJobs();
        }

        public IEnumerable<AudioRouteRow> Rows => Tab.RoutesList.ItemsSource!.Cast<AudioRouteRow>();

        public AudioStreamOption StreamOptionFor(AudioStream stream) =>
            Tab.StreamSelector.ItemsSource!.Cast<AudioStreamOption>().First(o => o.Stream.Id == stream.Id);

        public T FindRowControl<T>(string name) where T : Control =>
            Tab.RoutesList.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    }
}
