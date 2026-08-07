using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OMP.Lib.Session;
using OMP.Lib.Video;
using OMP.Ui.Controls;
using OMP.Ui.Input;

namespace OMP.Ui;

public partial class MainWindow : Window
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

    private readonly DispatcherTimer _uiTimer = new();
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IMainWindowCommands _commands;
    private readonly IMainWindowHotkeyService _hotkeyService;
    private readonly IWindowFactory _windowFactory;
    private readonly VideoRenderSurface _videoRenderSurface;
    private readonly FullscreenController _fullscreenController;

    public MainWindow(
        IMediaSessionRegistry mediaSessionRegistry,
        IMainWindowCommands commands,
        IMainWindowHotkeyService hotkeyService,
        IWindowFactory windowFactory)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        _commands = commands;
        _hotkeyService = hotkeyService;
        _windowFactory = windowFactory;
        InitializeComponent();

        _videoRenderSurface = new VideoRenderSurface(VideoView);
        _fullscreenController = new FullscreenController(this, TopMenu, OverlayControls, VideoSurface);

        _commands.Attach(new MainWindowCommandContext
        {
            GetIsPlaying = () => IsPlaying,
            GetIsFullscreen = () => _fullscreenController.IsFullscreen,
            SetIsPlaying = value => IsPlaying = value,
            ToggleFullscreen = () => _fullscreenController.Toggle()
        });
        SetupButtons();
        SetupUiTimer();
        SetupHotkeys();
        OverlayControls.SizeChanged += (_, _) => _fullscreenController.UpdateVideoViewportMargin();
        UpdatePlayPauseIcon();
        _fullscreenController.UpdateVideoViewportMargin();
        _mediaSessionRegistry.SessionChanged += OnSessionChanged;
        ProgressSlider.PointerCaptureLost += (_, _) =>
            _mediaSessionRegistry.Current?.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
        if (_mediaSessionRegistry.Current is not null)
        {
            OnSessionChanged(_mediaSessionRegistry);
            UpdateSessionData();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _fullscreenController.Dispose();
        _videoRenderSurface.Dispose();

        base.OnClosed(e);
    }

    private void OnSessionChanged(IMediaSessionRegistry registry)
    {
        registry.Current?.VideoFrameReady -= Render;
        registry.Current?.VideoFrameReady += Render;
        _videoRenderSurface.Reset();
        IsPlaying = false;
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

            if (!ProgressSlider.IsPointerOver)
            {
                ProgressSlider.Value = current;
            }

            CurrentTimeLabel.Text = FormatTime(session.CurrentTime);
        };

        _uiTimer.Start();
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

    private void SetupButtons()
    {
        OpenMenuItem.Click += async (_, _) => await OpenFile();
        ExitMenuItem.Click += (_, _) => Close();

        PlayPauseButton.Click += (_, _) => _commands.TogglePlayPause();
        StepBackButton.Click += (_, _) => _commands.StepBack();
        StepForwardButton.Click += (_, _) => _commands.StepForward();

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
        Title = $"{_mediaSessionRegistry.Current!.FileName} | Opportun Media Player";
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
        e.Handled = _hotkeyService.Handle(e.Key, _commands);
    }
}
