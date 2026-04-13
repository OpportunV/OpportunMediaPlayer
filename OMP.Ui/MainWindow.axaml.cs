using System;
using System.Linq;
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
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OMP.Ui;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _overlayTimer = new();
    private readonly DispatcherTimer _uiTimer = new();
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private bool _isFullscreen;
    private WindowState _previousWindowState;
    private bool _isPlaying;
    private WriteableBitmap? _videoBitmap;
    private int _uiFramesRendered;
    private double _uiFps;
    private readonly Stopwatch _uiFpsStopwatch = Stopwatch.StartNew();

    public MainWindow(IMediaSessionRegistry mediaSessionRegistry)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        InitializeComponent();
        SetupButtons();
        SetupOverlayTimer();
        SetupUiTimer();
        AddHotkeys();
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
            Title =
                $"{session.FileName} | Opportun Media Player | Decode {session.VideoDecodeFps:F1} | Dispatch {session.VideoFps:F1} | UI {_uiFps:F1} FPS | x{session.Speed:F2}";
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
            _uiFramesRendered++;
            if (_uiFpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                _uiFps = _uiFramesRendered * 1000.0 / _uiFpsStopwatch.ElapsedMilliseconds;
                _uiFramesRendered = 0;
                _uiFpsStopwatch.Restart();
            }
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

    private void SetupButtons()
    {
        OpenMenuItem.Click += async (_, _) => await OpenFile();
        ExitMenuItem.Click += (_, _) => Close();

        PlayPauseButton.Click += (_, _) => TogglePlayPause();
        StepBackButton.Click += (_, _) => _mediaSessionRegistry.Current?.Step(TimeSpan.FromSeconds(-5));
        StepForwardButton.Click += (_, _) => _mediaSessionRegistry.Current?.Step(TimeSpan.FromSeconds(5));

        FullscreenButton.Click += (_, _) => ToggleFullscreen();
        OptionsButton.Click += (_, _) => ShowOptionsWindow();
    }
    
    private void TogglePlayPause()
    {
        var session = _mediaSessionRegistry.Current;

        if (session == null)
        {
            return;
        }

        if (_isPlaying)
        {
            session.Pause();
            PlayPauseButton.Content = "▶";
            _isPlaying = false;
        }
        else
        {
            _isPlaying = true;
            session.Play();
            PlayPauseButton.Content = "⏸";
        }
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
            var session = _mediaSessionRegistry.Current!;

            var routes = session.AudioStreams
                .Zip(session.AudioOutputs)
                .ToList();
            
            Console.WriteLine($"Audio streams count: {routes.Count}");
            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    session.AudioStreams.Select(stream => $"{stream.Title}, {stream.Language}")));
            Console.WriteLine();
            Console.WriteLine($"Audio outputs count: {routes.Count}");
            Console.WriteLine(
                string.Join(Environment.NewLine, session.AudioOutputs.Select(output => output.FriendlyName)));
            
            Console.WriteLine("Resulting routes:");
            Console.WriteLine(
                string.Join(
                    Environment.NewLine,
                    routes.Select(output => $"{output.First.Title} -> {output.Second.FriendlyName}")));
            
            session.SetAudioRoutes(routes);
            
            UpdateSessionData();
            Console.WriteLine($"Total duration is {session.Duration}.");
        }
    }

    private void UpdateSessionData()
    {
        Title = $"{_mediaSessionRegistry.Current!.FileName} | Opportun Media Player";
        DurationLabel.Text = FormatTime(_mediaSessionRegistry.Current!.Duration);
        ProgressSlider.Maximum = _mediaSessionRegistry.Current?.Duration.TotalSeconds ?? 0;
        _mediaSessionRegistry.Current!.SetSpeed(1);
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

            _overlayTimer.Stop();
        }
    }


    private void ShowOptionsWindow()
    {
        var window = Program.Services.GetRequiredService<OptionsWindow>();
        window.ShowDialog(this);
    }

    private void AddHotkeys()
    {
        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Space:
                    PlayPauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;

                case Key.Left:
                    StepBackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;

                case Key.Right:
                    StepForwardButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;

                case Key.F:
                    FullscreenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;
                case Key.OemPlus:
                case Key.Add:
                    ChangeSpeed(0.1);
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    ChangeSpeed(-0.1);
                    break;
            }
        };
    }

    private void ChangeSpeed(double delta)
    {
        var session = _mediaSessionRegistry.Current;
        session?.SetSpeed(session.Speed + delta);
    }
}