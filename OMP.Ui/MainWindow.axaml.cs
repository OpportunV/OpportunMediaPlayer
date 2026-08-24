using System;
using System.Buffers;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OMP.Lib;
using OMP.Lib.Audio;
using OMP.Lib.Session;
using OMP.Lib.Video;
using OMP.Ui.Controls;
using OMP.Ui.Extensions;
using OMP.Ui.Input;
using OMP.Ui.Localization;
using OMP.Ui.Services;
using OMP.Ui.Settings;
using OMP.Ui.Windows;

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

    private bool _isSeekingViaSlider;
    private bool _areSubtitlesEnabled;
    private bool _hasShownAudioOutputWarning;
    private int _lastKnownSubtitleRouteCount;
    private int _sessionGeneration;
    private readonly DispatcherTimer _uiTimer = new();
    private readonly Action<IMediaSessionRegistry> _onSessionChanged;
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IMainWindowCommands _commands;
    private readonly IMainWindowHotkeyService _hotkeyService;
    private readonly IWindowFactory _windowFactory;
    private readonly IUserSettingsService _settings;
    private readonly ILogger<MainWindow> _logger;
    private readonly SingleInstanceCoordinator _singleInstanceCoordinator;
    private readonly VideoRenderSurface _videoRenderSurface;
    private readonly FullscreenController _fullscreenController;
    private readonly VolumeBarPresenter _volumeBar;
    private readonly SubtitleOverlayRenderer _subtitleOverlayRenderer;
    private readonly MediaOpener _mediaOpener;
    private readonly SpeedFlyoutView _speedFlyoutView = new();

    public MainWindow(
        IMediaSessionRegistry mediaSessionRegistry,
        IMainWindowCommands commands,
        IMainWindowHotkeyService hotkeyService,
        IWindowFactory windowFactory,
        IUserSettingsService settings,
        IYtDlpResolver ytDlpResolver,
        ILogger<MainWindow> logger,
        SingleInstanceCoordinator singleInstanceCoordinator,
        NativeLibraryOptions nativeLibraryOptions,
        StartupOptions startupOptions)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        _commands = commands;
        _hotkeyService = hotkeyService;
        _windowFactory = windowFactory;
        _settings = settings;
        _logger = logger;
        _singleInstanceCoordinator = singleInstanceCoordinator;
        InitializeComponent();
        Title = AppInfo.DisplayName;

        var windowGeometry = new WindowGeometryPersistence(this, settings, () => _fullscreenController!.IsFullscreen);
        windowGeometry.Restore();

        _videoRenderSurface = new VideoRenderSurface(VideoView);
        _fullscreenController = new FullscreenController(this, TopMenu, OverlayControls, VideoSurface);
        _subtitleOverlayRenderer = new SubtitleOverlayRenderer(SubtitleOverlay);
        _mediaOpener = new MediaOpener(
            this, LoadingIndicator, EmptyStateLabel, mediaSessionRegistry, ytDlpResolver, windowFactory,
            nativeLibraryOptions);
        _mediaOpener.MediaOpened += UpdateSessionData;

        _volumeBar = new VolumeBarPresenter(
            VolumeSlider, VolumeLabel, SpeakerIcon, SpeakerMutedIcon, commands, settings);

        _commands.Attach(
            new MainWindowCommandContext
            {
                GetIsPlaying = () => IsPlaying,
                GetIsFullscreen = () => _fullscreenController.IsFullscreen,
                SetIsPlaying = value => IsPlaying = value,
                SetIsMuted = value => _volumeBar.IsMuted = value,
                SetSpeedDisplay = OnSpeedChanged,
                SetVolumeDisplay = _volumeBar.OnVolumeChanged,
                ToggleFullscreen = () => _fullscreenController.Toggle(),
                ToggleSubtitles = () => SubtitlesButton.IsChecked = SubtitlesButton.IsChecked != true
            });
        SetupNativeMenu();
        SetupButtons();
        SetupSpeed();
        _ = new OutputVolumeFlyoutPresenter(OutputVolumesButton, mediaSessionRegistry, settings);
        SetupSubtitles();
        SetupUiTimer();
        SetupHotkeys();
        SetupProgressSlider();
        windowGeometry.StartPersisting();
        _singleInstanceCoordinator.StartWatchingForOpenRequests(HandleExternalOpenRequest);
        _onSessionChanged = registry => Dispatcher.UIThread.Post(() => OnSessionChanged(registry));
        _mediaSessionRegistry.SessionChanged += _onSessionChanged;

        if (_mediaSessionRegistry.Current is not null)
        {
            OnSessionChanged(_mediaSessionRegistry);
            UpdateSessionData();
        }
        else if (startupOptions.FilePath is { } startupFilePath)
        {
            Opened += async (_, _) => await _mediaOpener.OpenPathAsync(startupFilePath);
        }
    }

    internal void HandleExternalOpenRequest(string? path)
    {
        Activate();

        if (!string.IsNullOrEmpty(path))
        {
            _ = _mediaOpener.OpenPathAsync(path);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _mediaSessionRegistry.SessionChanged -= _onSessionChanged;
        _uiTimer.Stop();
        _mediaSessionRegistry.Close();
        _settings.Save();
        _fullscreenController.Dispose();
        _videoRenderSurface.Dispose();
        _singleInstanceCoordinator.Dispose();

        base.OnClosed(e);
    }

    private void OnSessionChanged(IMediaSessionRegistry registry)
    {
        _sessionGeneration++;

        registry.Current?.VideoFrameReady -= Render;
        registry.Current?.VideoFrameReady += Render;
        registry.Current?.PlaybackEnded -= OnPlaybackEnded;
        registry.Current?.PlaybackEnded += OnPlaybackEnded;
        _videoRenderSurface.Reset();
        _subtitleOverlayRenderer.Clear();
        IsPlaying = false;
        NoVideoIndicator.IsVisible = registry.Current is { HasVideo: false };
        var isEmpty = registry.Current is null;
        EmptyStateIndicator.IsVisible = isEmpty;

        OutputVolumesButton.IsVisible = !isEmpty;
        SpeedButton.IsVisible = !isEmpty;
        SubtitlesButton.IsVisible = !isEmpty;
        TransportGroup.Opacity = isEmpty ? 0.35 : 1;
        TimelineRow.Opacity = isEmpty ? 0.35 : 1;

        _areSubtitlesEnabled = false;
        _lastKnownSubtitleRouteCount = 0;
        SubtitlesButton.IsChecked = false;

        if (registry.Current is null)
        {
            return;
        }

        RestoreAudioRoutes(registry.Current);
        _volumeBar.RestoreVolume(registry.Current);

        _commands.SetSpeed(_settings.Current.PlaybackSpeed);

        if (!_hasShownAudioOutputWarning && registry.Current.AudioOutputUnavailableReason is { } reason)
        {
            _hasShownAudioOutputWarning = true;
            var warning = _windowFactory.Create<AudioOutputWarningWindow>();
            warning.Load(reason);
            warning.ShowDialog(this);
        }
    }

    private void SetupSpeed()
    {
        SpeedButton.Flyout = new Flyout
        {
            Content = _speedFlyoutView,
            Placement = PlacementMode.Top,
            FlyoutPresenterClasses = { "app-flyout" }
        };

        _speedFlyoutView.SpeedCommitted += speed => _commands.SetSpeed(speed);

        SetSpeedDisplayText(_settings.Current.PlaybackSpeed);
    }

    private void SetSpeedDisplayText(double speed)
    {
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

    private void RestoreAudioRoutes(IMediaSession session)
    {
        var preferred = _settings.Current.PreferredAudioTracks;

        if (preferred.Count == 0)
        {
            return;
        }

        var routes = AudioRouteMatcher.Match(
            session.AudioStreams,
            session.AudioOutputs,
            preferred.Select(p => new PreferredAudioTrack(p.OutputFriendlyName, p.Title, p.Language)).ToList());

        if (routes.Count > 0)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    session.SetAudioRoutes(routes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Restoring persisted audio routes failed.");
                }
            });
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

            CurrentTimeLabel.Text = session.CurrentTime.Format();

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
        var generation = _sessionGeneration;

        var buffer = ArrayPool<byte>.Shared.Rent(frame.DataLength);
        unsafe
        {
            fixed (byte* dst = buffer)
            {
                Buffer.MemoryCopy((void*)frame.DataPtr, dst, buffer.Length, frame.DataLength);
            }
        }

        var width = frame.Width;
        var height = frame.Height;
        var length = frame.DataLength;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (generation == _sessionGeneration)
                {
                    _videoRenderSurface.Render(width, height, buffer, length);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        });
    }

    private void OnPlaybackEnded()
    {
        Dispatcher.UIThread.Post(() => IsPlaying = false);
    }

    private void UpdatePlayPauseIcon()
    {
        PlayIcon.IsVisible = !IsPlaying;
        PauseIcon.IsVisible = IsPlaying;
    }

    private void SetupNativeMenu()
    {
        // Built in code, not XAML - elements declared inside Window.NativeMenu don't get compiled x:Name fields
        var openItem = new NativeMenuItem(Strings.MainWindow_OpenMenuItem);
        openItem.Click += async (_, _) => await _mediaOpener.OpenFileAsync();

        var openUrlItem = new NativeMenuItem(Strings.MainWindow_OpenUrlMenuItem);
        openUrlItem.Click += async (_, _) => await _mediaOpener.OpenUrlAsync();

        var optionsItem = new NativeMenuItem(Strings.MainWindow_OptionsMenuItem);
        optionsItem.Click += (_, _) => ShowOptionsWindow();

        var exitItem = new NativeMenuItem(Strings.MainWindow_ExitMenuItem);
        exitItem.Click += (_, _) => Close();

        var fileMenu = new NativeMenuItem(Strings.MainWindow_FileMenu)
        {
            Menu = [openItem, openUrlItem, optionsItem, new NativeMenuItemSeparator(), exitItem]
        };

        var hotkeysItem = new NativeMenuItem(Strings.MainWindow_HotkeysMenuItem);
        hotkeysItem.Click += (_, _) => _windowFactory.Create<HotkeysWindow>().Show(this);

        var aboutItem = new NativeMenuItem(Strings.MainWindow_AboutMenuItem);
        aboutItem.Click += (_, _) => _windowFactory.Create<AboutWindow>().ShowDialog(this);

        var helpMenu = new NativeMenuItem(Strings.MainWindow_HelpMenu)
        {
            Menu = [hotkeysItem, aboutItem]
        };

        NativeMenu.SetMenu(this, [fileMenu, helpMenu]);
    }

    private void SetupButtons()
    {
        PlayPauseButton.Click += (_, _) => _commands.TogglePlayPause();
        StepBackButton.Click += (_, _) => _commands.StepBack();
        StepForwardButton.Click += (_, _) => _commands.StepForward();

        MuteButton.Click += (_, _) => _commands.ToggleMute();
        FullscreenButton.Click += (_, _) => _commands.ToggleFullscreen();
        OptionsButton.Click += (_, _) => ShowOptionsWindow();

        VideoSurface.DoubleTapped += (_, _) => _commands.ToggleFullscreen();
    }

    private void SetupProgressSlider()
    {
        ProgressSlider.PointerMoved += (_, e) =>
        {
            if (_mediaSessionRegistry.Current == null || ProgressSlider.Bounds.Width <= 0)
            {
                return;
            }

            var ratio = Math.Clamp(e.GetPosition(ProgressSlider).X / ProgressSlider.Bounds.Width, 0, 1);
            var hoveredTime = TimeSpan.FromSeconds(ratio * ProgressSlider.Maximum);
            ToolTip.SetTip(ProgressSlider, hoveredTime.Format());
        };

        ProgressSlider.AddHandler(PointerPressedEvent, (_, _) => _isSeekingViaSlider = true, RoutingStrategies.Tunnel);
        ProgressSlider.PointerCaptureLost += (_, _) =>
        {
            _mediaSessionRegistry.Current?.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
            _isSeekingViaSlider = false;
        };
    }

    private void UpdateSessionData()
    {
        var displayName = _mediaOpener.ResolvedTitle ?? _mediaSessionRegistry.Current!.FileName;
        Title = $"{displayName} | {AppInfo.DisplayName}";
        DurationLabel.Text = _mediaSessionRegistry.Current!.Duration.Format();
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