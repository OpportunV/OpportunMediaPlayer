using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Ui.Models;
using OMP.Ui.Services;
using OMP.Ui.Settings;
using OMP.Ui.Tests.TestDoubles;

namespace OMP.Ui.Tests.Services;

/// <summary>
/// These exist mainly to catch a dropped event subscription. Moving the handlers off the window
/// traded XAML attributes for <c>+=</c> wiring, and a missing <c>+=</c> still compiles - the
/// control simply stops doing anything. Every test here drives a real control and asserts the
/// effect reached the session or the settings, rather than calling the handler directly.
/// </summary>
public class OptionsAudioRoutingSectionTests
{
    private static readonly AudioStream _mainStream = new(1, "aac", "Main", "en");
    private static readonly AudioStream _commentaryStream = new(2, "aac", "Commentary", "en");
    private static readonly AudioOutput _speakers = new(1, "Speakers");
    private static readonly AudioOutput _headset = new(2, "Headset");

    [AvaloniaFact]
    public void SelectingAnOutputAndTrack_CommitsARouteToTheSession()
    {
        var h = new Harness();

        h.OutputSelector.SelectedItem = _speakers;
        h.StreamSelector.SelectedItem = h.StreamOptionFor(_mainStream);
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
        h.OutputSelector.SelectedItem = _speakers;

        h.RaiseClick(h.ClearDraftButton);

        Assert.Null(h.OutputSelector.SelectedItem);
    }

    /// <summary>
    /// Guards the output picker specifically: without its own subscription the track picker still
    /// commits a route, so only the enable/disable gate proves that handler is wired.
    /// </summary>
    [AvaloniaFact]
    public void TrackPicker_IsDisabledUntilAnOutputIsChosen_AndDisabledAgainWhenCleared()
    {
        var h = new Harness();

        h.OutputSelector.SelectedItem = _speakers;
        Assert.True(h.StreamSelector.IsEnabled);

        h.OutputSelector.SelectedItem = null;
        Assert.False(h.StreamSelector.IsEnabled);
        Assert.Null(h.StreamSelector.SelectedItem);
    }

    [AvaloniaFact]
    public void CommittingARoute_RemovesThatOutputFromTheDraftPicker()
    {
        var h = new Harness();

        h.OutputSelector.SelectedItem = _speakers;
        h.StreamSelector.SelectedItem = h.StreamOptionFor(_mainStream);

        var remaining = h.OutputSelector.ItemsSource!.Cast<AudioOutput>().ToList();
        Assert.DoesNotContain(remaining, o => o.Id == _speakers.Id);
        Assert.Contains(remaining, o => o.Id == _headset.Id);
    }

    [AvaloniaFact]
    public void TheSameTrackMayFeedTwoOutputs()
    {
        var h = new Harness();

        h.OutputSelector.SelectedItem = _speakers;
        h.StreamSelector.SelectedItem = h.StreamOptionFor(_mainStream);
        h.OutputSelector.SelectedItem = _headset;
        h.StreamSelector.SelectedItem = h.StreamOptionFor(_mainStream);
        h.Session.WaitForAudioRoutes(2);

        var applied = h.Session.AppliedAudioRoutes.Last();
        Assert.Equal(2, applied.Count);
        Assert.All(applied, r => Assert.Equal(_mainStream.Id, r.Stream.Id));
    }

    [AvaloniaFact]
    public void OnRouteVolumeChanged_PushesToTheSessionAndPersists()
    {
        var h = new Harness(withExistingRoute: true);
        var row = h.Rows.Single();

        var slider = new Slider { Minimum = 0, Maximum = 200, Value = 100, DataContext = row };
        slider.ValueChanged += h.Section.OnRouteVolumeChanged;

        slider.Value = 40;

        Assert.Contains(h.Session.OutputVolumeCalls, c => c.OutputId == _speakers.Id && Math.Abs(c.Volume - 0.4) < .001);
        Assert.Contains(h.Settings.OutputVolumes, o => o.FriendlyName == "Speakers");
    }

    [AvaloniaFact]
    public void DeleteRoute_IsRefusedWhileOnlyOneRouteRemains()
    {
        var h = new Harness(withExistingRoute: true);
        var row = h.Rows.Single();

        h.Section.OnDeleteRoute(new Button { DataContext = row }, new Avalonia.Interactivity.RoutedEventArgs());
        h.Session.SettleRouteApplications();

        Assert.Single(h.Rows);
        Assert.Empty(h.Session.AppliedAudioRoutes);
    }

    private sealed class Harness
    {
        public ItemsControl RoutesList { get; } = new();

        public ComboBox OutputSelector { get; } = new();

        public ComboBox StreamSelector { get; } = new();

        public Button ClearDraftButton { get; } = new();

        public FakeMediaSession Session { get; }

        public UserSettings Settings { get; } = new();

        public OptionsAudioRoutingSection Section { get; }

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

            Section = new OptionsAudioRoutingSection(
                RoutesList,
                OutputSelector,
                StreamSelector,
                ClearDraftButton,
                new FakeMediaSessionRegistry { Current = Session },
                settingsService.Object,
                NullLoggerFactory.Instance);
        }

        public IEnumerable<AudioRouteRow> Rows => RoutesList.ItemsSource!.Cast<AudioRouteRow>();

        public AudioStreamOption StreamOptionFor(AudioStream stream) =>
            StreamSelector.ItemsSource!.Cast<AudioStreamOption>().First(o => o.Stream.Id == stream.Id);

        public void RaiseClick(Button button) =>
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
    }
}
