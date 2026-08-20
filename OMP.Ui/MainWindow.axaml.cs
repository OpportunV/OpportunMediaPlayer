using System;
using System.Buffers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OMP.Lib;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Session;
using OMP.Lib.Video;
using OMP.Ui.Controls;
using OMP.Ui.Extensions;
using OMP.Ui.Helpers;
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

    private static readonly FilePickerFileType _mediaFileTypeFilter = new(Strings.MainWindow_OpenFileTypeFilterName)
    {
        Patterns =
        [
            "*.mp4", "*.mkv", "*.avi", "*.webm", "*.mov", "*.flv", "*.wmv",
            "*.mp3", "*.flac", "*.wav", "*.ogg", "*.m4a", "*.aac"
        ]
    };

    private bool _isSeekingViaSlider;
    private bool _areSubtitlesEnabled;
    private bool _hasShownAudioOutputWarning;
    private bool _isResolvingUrl;
    private int _lastKnownSubtitleRouteCount;
    private int _sessionGeneration;
    private string? _resolvedTitleOverride;
    private readonly DispatcherTimer _uiTimer = new();
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IMainWindowCommands _commands;
    private readonly IMainWindowHotkeyService _hotkeyService;
    private readonly IWindowFactory _windowFactory;
    private readonly IUserSettingsService _settings;
    private readonly IYtDlpResolver _ytDlpResolver;
    private readonly SingleInstanceCoordinator _singleInstanceCoordinator;
    private readonly NativeLibraryOptions _nativeLibraryOptions;
    private readonly VideoRenderSurface _videoRenderSurface;
    private readonly FullscreenController _fullscreenController;
    private readonly SubtitleOverlayRenderer _subtitleOverlayRenderer;
    private readonly SpeedFlyoutView _speedFlyoutView = new();
    private readonly VolumeFlyoutView _volumeFlyoutView = new();

    public MainWindow(
        IMediaSessionRegistry mediaSessionRegistry,
        IMainWindowCommands commands,
        IMainWindowHotkeyService hotkeyService,
        IWindowFactory windowFactory,
        IUserSettingsService settings,
        IYtDlpResolver ytDlpResolver,
        SingleInstanceCoordinator singleInstanceCoordinator,
        NativeLibraryOptions nativeLibraryOptions,
        StartupOptions startupOptions)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        _commands = commands;
        _hotkeyService = hotkeyService;
        _windowFactory = windowFactory;
        _settings = settings;
        _ytDlpResolver = ytDlpResolver;
        _singleInstanceCoordinator = singleInstanceCoordinator;
        _nativeLibraryOptions = nativeLibraryOptions;
        InitializeComponent();
        Title = AppInfo.DisplayName;
        RestoreWindowGeometry();

        _videoRenderSurface = new VideoRenderSurface(VideoView);
        _fullscreenController = new FullscreenController(this, TopMenu, OverlayControls, VideoSurface);
        _subtitleOverlayRenderer = new SubtitleOverlayRenderer(SubtitleOverlay);

        _commands.Attach(
            new MainWindowCommandContext
            {
                GetIsPlaying = () => IsPlaying,
                GetIsFullscreen = () => _fullscreenController.IsFullscreen,
                SetIsPlaying = value => IsPlaying = value,
                SetIsMuted = value => IsMuted = value,
                SetSpeedDisplay = OnSpeedChanged,
                SetVolumeDisplay = OnVolumeChanged,
                ToggleFullscreen = () => _fullscreenController.Toggle(),
                ToggleSubtitles = () => SubtitlesButton.IsChecked = SubtitlesButton.IsChecked != true
            });
        SetupNativeMenu();
        SetupButtons();
        SetupVolume();
        SetupSpeed();
        SetupOutputVolumePopup();
        SetupSubtitles();
        SetupUiTimer();
        SetupHotkeys();
        SetupDragDrop();
        SetupVideoDoubleClick();
        SetupProgressSlider();
        SetupWindowGeometryPersistence();
        _singleInstanceCoordinator.StartWatchingForOpenRequests(HandleExternalOpenRequest);
        OverlayControls.SizeChanged += (_, _) => _fullscreenController.UpdateVideoViewportMargin();
        UpdatePlayPauseIcon();
        UpdateMuteIcon();
        _fullscreenController.UpdateVideoViewportMargin();
        _mediaSessionRegistry.SessionChanged += OnSessionChanged;

        if (_mediaSessionRegistry.Current is not null)
        {
            OnSessionChanged(_mediaSessionRegistry);
            UpdateSessionData();
        }
        else if (startupOptions.FilePath is { } startupFilePath)
        {
            Opened += async (_, _) => await OpenPath(startupFilePath);
        }
    }

    internal void HandleExternalOpenRequest(string? path)
    {
        Activate();

        if (!string.IsNullOrEmpty(path))
        {
            _ = OpenPath(path);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
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
        EmptyStateIndicator.IsVisible = registry.Current is null;

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

    private void SetupOutputVolumePopup()
    {
        var flyout = new Flyout
        {
            Content = _volumeFlyoutView,
            Placement = PlacementMode.Top,
            FlyoutPresenterClasses = { "app-flyout" }
        };
        flyout.Opened += (_, _) => RefreshOutputVolumeRows();
        OutputVolumesButton.Flyout = flyout;

        _volumeFlyoutView.OutputVolumeChanged += (output, volume) =>
            _mediaSessionRegistry.Current?.SetOutputVolume(output.Id, volume / 100);

        _volumeFlyoutView.OutputVolumeCommitted += PersistOutputVolumeSetting;

        _volumeFlyoutView.OutputMuteChanged += (output, muted) =>
        {
            _mediaSessionRegistry.Current?.SetOutputMuted(output.Id, muted);
            PersistOutputVolumeSetting(output);
        };
    }

    private void RefreshOutputVolumeRows()
    {
        var session = _mediaSessionRegistry.Current;

        if (session == null)
        {
            _volumeFlyoutView.SetOutputs([]);
            return;
        }

        _volumeFlyoutView.SetOutputs(session.AudioRoutes.ToVolumeRows(session.OutputVolumes));
    }

    private void PersistOutputVolumeSetting(AudioOutput output)
    {
        var session = _mediaSessionRegistry.Current;

        if (session != null && session.OutputVolumes.TryGetValue(output.Id, out var state))
        {
            _settings.UpsertOutputVolumeSetting(output, state.Volume * 100, state.Muted);
        }

        _settings.Save();
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

    private void OnVolumeChanged(double volume)
    {
        VolumeSlider.Value = volume * 100;
        VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        _settings.Current.MasterVolume = volume;
        _settings.Save();
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
            session.SetAudioRoutes(routes);
        }
    }

    private void RestoreVolume(IMediaSession session)
    {
        session.SetMasterVolume(_settings.Current.MasterVolume);
        session.SetMasterMuted(_settings.Current.IsMuted);
        IsMuted = _settings.Current.IsMuted;

        foreach (var (output, setting) in session.AudioOutputs.MatchSettings(_settings.Current.OutputVolumes))
        {
            session.SetOutputVolume(output.Id, setting.Volume);
            session.SetOutputMuted(output.Id, setting.Muted);
            session.SetOutputDelay(output.Id, setting.DelayMs / 1000.0);
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

    private void UpdateMuteIcon()
    {
        SpeakerIcon.IsVisible = !IsMuted;
        SpeakerMutedIcon.IsVisible = IsMuted;
    }

    private void SetupNativeMenu()
    {
        // Built in code, not XAML - elements declared inside Window.NativeMenu don't get compiled x:Name fields
        var openItem = new NativeMenuItem(Strings.MainWindow_OpenMenuItem);
        openItem.Click += async (_, _) => await OpenFile();

        var openUrlItem = new NativeMenuItem(Strings.MainWindow_OpenUrlMenuItem);
        openUrlItem.Click += async (_, _) => await OpenUrl();

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
    }

    private async Task OpenFile()
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = Strings.MainWindow_OpenFileDialogTitle,
                AllowMultiple = false,
                FileTypeFilter = [_mediaFileTypeFilter]
            });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();

        if (path == null)
        {
            return;
        }

        await OpenPath(path);
    }

    private async Task OpenPath(string path)
    {
        _resolvedTitleOverride = null;

        if (await OpenSessionOrShowError(path))
        {
            UpdateSessionData();
        }
    }

    private async Task OpenUrl()
    {
        if (_isResolvingUrl)
        {
            return;
        }

        _isResolvingUrl = true;

        try
        {
            string? prefillUrl = null;

            while (true)
            {
                var dialog = _windowFactory.Create<OpenUrlWindow>();
                dialog.Load(prefillUrl);

                var result = await dialog.ShowDialog<YtDlpResolveResult?>(this);

                if (result is null)
                {
                    return;
                }

                _resolvedTitleOverride = result.Title;

                if (TryOpenSessionSilently(result.Url!, out _))
                {
                    UpdateSessionData();
                    return;
                }

                var retryResult = await _ytDlpResolver.ResolveAsync(result.PageUrl, CancellationToken.None);
                var retryUrl = result.Url!;

                if (retryResult.Status == YtDlpResolveStatus.Success)
                {
                    _resolvedTitleOverride = retryResult.Title;
                    retryUrl = retryResult.Url!;
                }

                if (await OpenSessionOrShowError(retryUrl))
                {
                    UpdateSessionData();
                    return;
                }

                prefillUrl = result.PageUrl;
            }
        }
        finally
        {
            _isResolvingUrl = false;
        }
    }

    private async Task<bool> OpenSessionOrShowError(string mediaPath)
    {
        if (TryOpenSessionSilently(mediaPath, out var error))
        {
            return true;
        }

        var heading = OperatingSystem.IsMacOS() && _nativeLibraryOptions.FFmpegLibraryDirectory is null
            ? Strings.OpenFileError_FFmpegMacHeading
            : Strings.OpenFileError_Heading;

        await ShowError(heading, error!.Message);
        return false;
    }

    private bool TryOpenSessionSilently(string mediaPath, out Exception? error)
    {
        try
        {
            _mediaSessionRegistry.Open(mediaPath);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    private async Task ShowError(string heading, string reason)
    {
        var errorWindow = _windowFactory.Create<OpenFileErrorWindow>();
        errorWindow.Load(heading, reason);
        await errorWindow.ShowDialog(this);
    }

    private void SetupDragDrop()
    {
        if (OperatingSystem.IsLinux())
        {
            EmptyStateLabel.Text = Strings.MainWindow_EmptyStateLabelNoDragDrop;
            return;
        }

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnFileDrop);
        AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
    }

    private void SetupVideoDoubleClick()
    {
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

    private void RestoreWindowGeometry()
    {
        var window = _settings.Current.Window;

        if (window is { Width: { } width, Height: { } height })
        {
            Width = width;
            Height = height;
        }

        if (window is { Left: { } left, Top: { } top })
        {
            var position = new PixelPoint((int)left, (int)top);

            if (IsPositionOnAnyScreen(position))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = position;
            }
        }

        if (window.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private bool IsPositionOnAnyScreen(PixelPoint position)
    {
        try
        {
            return Screens.All.Count == 0 || Screens.All.Any(screen => screen.Bounds.Contains(position));
        }
        catch (Exception)
        {
            return true;
        }
    }

    private void SetupWindowGeometryPersistence()
    {
        PositionChanged += (_, _) =>
        {
            if (WindowState != WindowState.Normal || _fullscreenController.IsFullscreen)
            {
                return;
            }

            _settings.Current.Window.Left = Position.X;
            _settings.Current.Window.Top = Position.Y;
        };

        Resized += (_, _) =>
        {
            if (WindowState != WindowState.Normal || _fullscreenController.IsFullscreen)
            {
                return;
            }

            _settings.Current.Window.Width = Width;
            _settings.Current.Window.Height = Height;
        };

        PropertyChanged += (_, e) =>
        {
            if (_fullscreenController.IsFullscreen || e.Property != WindowStateProperty)
            {
                return;
            }

            if (WindowState is WindowState.Normal or WindowState.Maximized)
            {
                _settings.Current.Window.IsMaximized = WindowState == WindowState.Maximized;
            }
        };
    }

    private static void OnFileDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var path = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();

        if (path == null || !MediaFileType.IsSupportedMediaFile(path, _mediaFileTypeFilter.Patterns!))
        {
            return;
        }

        Activate();
        await OpenPath(path);
    }

    private void UpdateSessionData()
    {
        var displayName = _resolvedTitleOverride ?? _mediaSessionRegistry.Current!.FileName;
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