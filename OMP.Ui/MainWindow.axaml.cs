using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
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
    private int _lastKnownSubtitleRouteCount;
    private int _sessionGeneration;
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
        IUserSettingsService settings,
        StartupOptions startupOptions)
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
            SetVolumeDisplay = OnVolumeChanged,
            ToggleFullscreen = () => _fullscreenController.Toggle(),
            ToggleSubtitles = () => SubtitlesButton.IsChecked = SubtitlesButton.IsChecked != true
        });
        SetupButtons();
        SetupVolume();
        SetupSpeed();
        SetupSubtitles();
        SetupUiTimer();
        SetupHotkeys();
        SetupDragDrop();
        SetupVideoDoubleClick();
        SetupProgressSlider();
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

    protected override void OnClosed(EventArgs e)
    {
        _settings.Save();
        _fullscreenController.Dispose();
        _videoRenderSurface.Dispose();

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
            FlyoutPresenterClasses = { "speed-flyout" }
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

    private void OnVolumeChanged(double volume)
    {
        VolumeSlider.Value = volume * 100;
        VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        _settings.Current.MasterVolume = volume;
        _settings.Save();
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
            session.SetOutputDelay(output.Id, saved.DelayMs / 1000.0);
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
        OptionsMenuItem.Click += (_, _) => ShowOptionsWindow();
        ExitMenuItem.Click += (_, _) => Close();
        HotkeysMenuItem.Click += (_, _) => _windowFactory.Create<HotkeysWindow>().Show(this);
        AboutMenuItem.Click += (_, _) => _windowFactory.Create<AboutWindow>().ShowDialog(this);

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
        try
        {
            _mediaSessionRegistry.Open(path);
        }
        catch (Exception ex)
        {
            var errorWindow = _windowFactory.Create<OpenFileErrorWindow>();
            errorWindow.Load(ex.Message);
            await errorWindow.ShowDialog(this);
            return;
        }

        UpdateSessionData();
    }

    private void SetupDragDrop()
    {
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
            ToolTip.SetTip(ProgressSlider, FormatTime(hoveredTime));
        };

        ProgressSlider.AddHandler(PointerPressedEvent, (_, _) => _isSeekingViaSlider = true, RoutingStrategies.Tunnel);
        ProgressSlider.PointerCaptureLost += (_, _) =>
        {
            _mediaSessionRegistry.Current?.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
            _isSeekingViaSlider = false;
        };
    }

    private static void OnFileDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var path = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();

        if (path == null || !IsSupportedMediaFile(path))
        {
            return;
        }

        Activate();
        await OpenPath(path);
    }

    private static bool IsSupportedMediaFile(string path)
    {
        var extension = Path.GetExtension(path);

        return !string.IsNullOrEmpty(extension) && _mediaFileTypeFilter.Patterns!.Any(pattern => pattern.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
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
