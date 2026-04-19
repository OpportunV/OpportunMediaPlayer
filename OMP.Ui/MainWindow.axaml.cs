using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using OMP.Lib.Session;
using OMP.Lib.Video;
using OMP.Ui.Controls;
using OMP.Ui.Input;

namespace OMP.Ui;

public partial class MainWindow : Window
{
    private const double WindowedOverlaySpacing = 8;
    private readonly DispatcherTimer _overlayTimer = new();
    private readonly DispatcherTimer _uiTimer = new();
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly IMainWindowCommands _commands;
    private readonly IMainWindowHotkeyService _hotkeyService;
    private bool _isFullscreen;
    private WindowState _previousWindowState;
    private WriteableBitmap? _videoBitmap;

    private bool IsPlaying
    {
        get;
        set
        {
            field = value;
            UpdatePlayPauseIcon();
        }
    }

    public MainWindow(
        IMediaSessionRegistry mediaSessionRegistry,
        IMainWindowCommands commands,
        IMainWindowHotkeyService hotkeyService)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        _commands = commands;
        _hotkeyService = hotkeyService;
        InitializeComponent();
        _commands.Attach(new MainWindowCommandContext
        {
            GetIsPlaying = () => IsPlaying,
            SetIsPlaying = value => IsPlaying = value,
            ToggleFullscreen = ToggleFullscreen
        });
        SetupButtons();
        SetupOverlayTimer();
        SetupUiTimer();
        SetupHotkeys();
        OverlayControls.SizeChanged += (_, _) => UpdateVideoViewportMargin();
        UpdatePlayPauseIcon();
        UpdateVideoViewportMargin();
        _previousWindowState = WindowState;
        _mediaSessionRegistry.SessionChanged += OnSessionChanged;
        ProgressSlider.PointerCaptureLost += (_, _) =>
            _mediaSessionRegistry.Current?.Seek(TimeSpan.FromSeconds(ProgressSlider.Value));
        if (_mediaSessionRegistry.Current is not null)
        {
            OnSessionChanged(_mediaSessionRegistry);
            UpdateSessionData();
        }
    }

    private void OnSessionChanged(IMediaSessionRegistry registry)
    {
        registry.Current?.VideoFrameReady -= Render;
        registry.Current?.VideoFrameReady += Render;
        _videoBitmap = null;
        VideoView.Source = null;
        IsPlaying = false;
    }

    private void OnPointerExited(object? o, PointerEventArgs pointerEventArgs)
    {
        if (!_isFullscreen)
        {
            return;
        }

        TopMenu.IsVisible = false;
        OverlayControls.Opacity = 0;

        _overlayTimer.Stop();
    }

    private void OnPointerMoved(object? o, PointerEventArgs pointerEventArgs)
    {
        if (!_isFullscreen)
        {
            return;
        }

        TopMenu.Opacity = 1;
        OverlayControls.Opacity = 1;

        _overlayTimer.Stop();
        _overlayTimer.Start();
    }

    private void SetupOverlayTimer()
    {
        _overlayTimer.Interval = TimeSpan.FromSeconds(3);
        _overlayTimer.Tick += (_, _) =>
        {
            if (_isFullscreen)
            {
                OverlayControls.Opacity = 0;
                _overlayTimer.Stop();
            }
        };
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
        Dispatcher.UIThread.Post(() =>
        {
            if (_videoBitmap == null ||
                _videoBitmap.PixelSize.Width != frame.Width ||
                _videoBitmap.PixelSize.Height != frame.Height)
            {
                _videoBitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Premul);

                VideoView.Source = _videoBitmap;
            }

            using var fb = _videoBitmap.Lock();

            unsafe
            {
                Buffer.MemoryCopy((void*)frame.DataPtr, (void*)fb.Address, frame.DataLength, frame.DataLength);
            }

            VideoView.InvalidateVisual();
        });
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

    private void UpdateVideoViewportMargin()
    {
        var bottomMargin = _isFullscreen ? 0 : OverlayControls.Bounds.Height + WindowedOverlaySpacing;
        VideoSurface.Margin = new Thickness(0, 0, 0, bottomMargin);
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

    private void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;
        if (_isFullscreen)
        {
            _previousWindowState = WindowState;
            PointerMoved += OnPointerMoved;
            PointerExited += OnPointerExited;
            SystemDecorations = SystemDecorations.None;
            WindowState = WindowState.FullScreen;

            TopMenu.IsVisible = false;
            OverlayControls.Opacity = 1;
            UpdateVideoViewportMargin();

            _overlayTimer.Start();
        }
        else
        {
            PointerMoved -= OnPointerMoved;
            PointerExited -= OnPointerExited;
            SystemDecorations = SystemDecorations.Full;
            WindowState = _previousWindowState;

            TopMenu.IsVisible = true;
            OverlayControls.Opacity = 1;
            UpdateVideoViewportMargin();

            _overlayTimer.Stop();
        }
    }

    private void ShowOptionsWindow()
    {
        var window = Program.Services.GetRequiredService<OptionsWindow>();
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
