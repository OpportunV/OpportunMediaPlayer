using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OMP.Lib.Audio;
using OMP.Lib.Session;
using OMP.Lib.Video;
using OMP.Ui.Controls;
using OMP.Ui.Extensions;
using OMP.Ui.Input;
using OMP.Ui.Settings;

namespace OMP.Ui;

public sealed partial class MainWindow : Window
{
    private bool IsPlaying
    {
        get;
        set
        {
            field = value;
            UpdatePlayPauseIcon();
        }
    }

    private bool IsMuted
    {
        get;
        set
        {
            field = value;
            _settings.Current.IsMuted = value;
            UpdateMuteIcon();
        }
    }

    private bool _isSeekingViaSlider;
    private bool _areSubtitlesEnabled;
    private int _lastKnownSubtitleRouteCount;
    private readonly DispatcherTimer _uiTimer = new();
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IMainWindowCommands _commands;
    private readonly IMainWindowHotkeyService _hotkeyService;
    private readonly IWindowFactory _windowFactory;
    private readonly IUserSettingsService _settings;
    private readonly VideoRenderSurface _videoRenderSurface;
    private readonly FullscreenController _fullscreenController;
    private readonly SubtitleOverlayRenderer _subtitleOverlayRenderer;
    private readonly SpeedFlyoutView _speedFlyoutView = new();

    public MainWindow(
        IMediaSessionRegistry mediaSessionRegistry,
        IMainWindowCommands commands,
        IMainWindowHotkeyService hotkeyService,
        IWindowFactory windowFactory,
        IUserSettingsService settings)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        _commands = commands;
        _hotkeyService = hotkeyService;
        _windowFactory = windowFactory;
        _settings = settings;
        InitializeComponent();
        Title = AppInfo.DisplayName;

        _videoRenderSurface = new VideoRenderSurface(VideoView);
        _fullscreenController = new FullscreenController(this, TopMenu, OverlayControls, VideoSurface);
        _subtitleOverlayRenderer = new SubtitleOverlayRenderer(SubtitleOverlay);

        _commands.Attach(new MainWindowCommandContext
        {
            GetIsPlaying = () => IsPlaying,
            GetIsFullscreen = () => _fullscreenController.IsFullscreen,
            SetIsPlaying = value => IsPlaying = value,
            SetIsMuted = value => IsMuted = value,
            SetSpeedDisplay = OnSpeedChanged,
            ToggleFullscreen = () => _fullscreenController.Toggle(),
            ToggleSubtitles = () => SubtitlesButton.IsChecked = SubtitlesButton.IsChecked != true
        });
        SetupButtons();
        SetupVolume();
        SetupSpeed();
        SetupSubtitles();
        SetupUiTimer();
        SetupHotkeys();
        OverlayControls.SizeChanged += (_, _) => _fullscreenController.UpdateVideoViewportMargin();
        UpdatePlayPauseIcon();
        UpdateMuteIcon();
        _fullscreenController.UpdateVideoViewportMargin();
        _mediaSessionRegistry.SessionChanged += OnSessionChanged;

        ProgressSlider.AddHandler(PointerPressedEvent, (_, _) => _isSeekingViaSlider = true, RoutingStrategies.Tunnel);
        ProgressSlider.PointerCaptureLost += (_, _) =>
        {
            _mediaSessionRegistry.Current?.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
            _isSeekingViaSlider = false;
        };

        if (_mediaSessionRegistry.Current is not null)
        {
            OnSessionChanged(_mediaSessionRegistry);
            UpdateSessionData();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _settings.Save();
        _fullscreenController.Dispose();
        _videoRenderSurface.Dispose();

        base.OnClosed(e);
    }

    private void OnSessionChanged(IMediaSessionRegistry registry)
    {
        registry.Current?.VideoFrameReady -= Render;
        registry.Current?.VideoFrameReady += Render;
        _videoRenderSurface.Reset();
        _subtitleOverlayRenderer.Clear();
        IsPlaying = false;

        _areSubtitlesEnabled = false;
        _lastKnownSubtitleRouteCount = 0;
        SubtitlesButton.IsChecked = false;

        if (registry.Current is null)
        {
            return;
        }

        RestoreAudioRoutes(registry.Current);
        RestoreVolume(registry.Current);

        _commands.SetSpeed(_settings.Current.PlaybackSpeed);
    }

    private void SetupSpeed()
    {
        SpeedButton.Flyout = new Flyout
        {
            Content = _speedFlyoutView,
            Placement = PlacementMode.Top
        };

        _speedFlyoutView.SpeedCommitted += speed => _commands.SetSpeed(speed);

        SetSpeedDisplayText(_settings.Current.PlaybackSpeed);
    }

    private void SetSpeedDisplayText(double speed)
    {
        SpeedLabel.Text = PlaybackSpeedFormat.Format(speed);
        _speedFlyoutView.SetSpeed(speed);
    }

    private void OnSpeedChanged(double speed)
    {
        SetSpeedDisplayText(speed);
        _settings.Current.PlaybackSpeed = speed;
        _settings.Save();
    }

    private void SetupSubtitles()
    {
        SubtitlesButton.IsChecked = _areSubtitlesEnabled;

        SubtitlesButton.IsCheckedChanged += (_, _) =>
        {
            _areSubtitlesEnabled = SubtitlesButton.IsChecked == true;

            if (!_areSubtitlesEnabled)
            {
                _subtitleOverlayRenderer.Clear();
            }
        };
    }

    private void SetupVolume()
    {
        VolumeSlider.Value = _settings.Current.MasterVolume * 100;
        VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        IsMuted = _settings.Current.IsMuted;

        VolumeSlider.ValueChanged += (_, e) =>
        {
            _commands.SetMasterVolume(e.NewValue / 100);
            _settings.Current.MasterVolume = e.NewValue / 100;
            VolumeLabel.Text = $"{(int)e.NewValue}%";
        };

        VolumeSlider.PointerCaptureLost += (_, _) => _settings.Save();
    }

    private void RestoreAudioRoutes(IMediaSession session)
    {
        var preferred = _settings.Current.PreferredAudioOutputs;

        if (preferred.Count == 0)
        {
            return;
        }

        var routes = new List<AudioRoute>();

        for (var i = 0; i < session.AudioStreams.Count && i < preferred.Count; i++)
        {
            var output = session.AudioOutputs.FirstOrDefault(o => o.FriendlyName == preferred[i]);

            if (output is not null)
            {
                routes.Add(new AudioRoute(session.AudioStreams[i], output));
            }
        }

        if (routes.Count > 0)
        {
            session.SetAudioRoutes(routes);
        }
    }

    private void RestoreVolume(IMediaSession session)
    {
        session.SetMasterVolume(_settings.Current.MasterVolume);
        session.SetMasterMuted(_settings.Current.IsMuted);
        IsMuted = _settings.Current.IsMuted;

        foreach (var saved in _settings.Current.OutputVolumes)
        {
            var output = session.AudioOutputs.FirstOrDefault(o => o.FriendlyName == saved.FriendlyName);

            if (output is null)
            {
                continue;
            }

            session.SetOutputVolume(output.Id, saved.Volume);
            session.SetOutputMuted(output.Id, saved.Muted);
        }
    }

    private void SetupUiTimer()
    {
        _uiTimer.Interval = TimeSpan.FromMilliseconds(200);

        _uiTimer.Tick += (_, _) =>
        {
            var session = _mediaSessionRegistry.Current;

            if (session == null)
            {
                return;
            }

            var current = session.CurrentTime.TotalSeconds;

            if (!_isSeekingViaSlider)
            {
                ProgressSlider.Value = current;
            }

            CurrentTimeLabel.Text = FormatTime(session.CurrentTime);

            var subtitleRouteCount = session.SubtitleRoutes.Count;
            if (subtitleRouteCount > 0 && _lastKnownSubtitleRouteCount == 0)
            {
                _areSubtitlesEnabled = true;
                SubtitlesButton.IsChecked = true;
            }

            _lastKnownSubtitleRouteCount = subtitleRouteCount;

            if (_areSubtitlesEnabled)
            {
                UpdateSubtitleOverlay(session);
            }
        };

        _uiTimer.Start();
    }

    private void UpdateSubtitleOverlay(IMediaSession session)
    {
        var cues = session.GetActiveSubtitleCues();
        var videoContentRect = _videoRenderSurface.GetVideoContentRect(VideoSurface.Bounds.Size);
        _subtitleOverlayRenderer.Render(cues, _settings.Current.SubtitleZones, videoContentRect);
    }

    private void Render(VideoFrame frame)
    {
        Dispatcher.UIThread.Post(() => _videoRenderSurface.Render(frame));
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return time.ToString(@"hh\:mm\:ss");
        }

        return time.ToString(@"mm\:ss");
    }

    private void UpdatePlayPauseIcon()
    {
        PlayIcon.IsVisible = !IsPlaying;
        PauseIcon.IsVisible = IsPlaying;
    }

    private void UpdateMuteIcon()
    {
        SpeakerIcon.IsVisible = !IsMuted;
        SpeakerMutedIcon.IsVisible = IsMuted;
    }

    private void SetupButtons()
    {
        OpenMenuItem.Click += async (_, _) => await OpenFile();
        ExitMenuItem.Click += (_, _) => Close();

        PlayPauseButton.Click += (_, _) => _commands.TogglePlayPause();
        StepBackButton.Click += (_, _) => _commands.StepBack();
        StepForwardButton.Click += (_, _) => _commands.StepForward();

        MuteButton.Click += (_, _) => _commands.ToggleMute();
        FullscreenButton.Click += (_, _) => _commands.ToggleFullscreen();
        OptionsButton.Click += (_, _) => ShowOptionsWindow();
    }

    private async Task OpenFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open media file",
                AllowMultiple = false
            });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();

        if (path != null)
        {
            _mediaSessionRegistry.Open(path);
            UpdateSessionData();
        }
    }

    private void UpdateSessionData()
    {
        Title = $"{_mediaSessionRegistry.Current!.FileName} | {AppInfo.DisplayName}";
        DurationLabel.Text = FormatTime(_mediaSessionRegistry.Current!.Duration);
        ProgressSlider.Maximum = _mediaSessionRegistry.Current?.Duration.TotalSeconds ?? 0;
    }

    private void ShowOptionsWindow()
    {
        var window = _windowFactory.Create<OptionsWindow>();
        window.ShowDialog(this);
    }

    private void SetupHotkeys()
    {
        AddHandler(
            KeyDownEvent,
            OnHotkeyPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    private void OnHotkeyPressed(object? sender, KeyEventArgs e)
    {
        e.Handled = _hotkeyService.Handle(e.Key, e.KeyModifiers, _commands);
    }
}
