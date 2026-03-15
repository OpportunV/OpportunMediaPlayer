using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace OMP.Ui;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _overlayTimer = new();
    private bool _isFullscreen;
    private WindowState _previousWindowState; 

    public MainWindow()
    {
        InitializeComponent();
        SetupButtons();
        SetupTimer();
        AddHotkeys();
        _previousWindowState = WindowState;
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

    private void SetupTimer()
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

    private void SetupButtons()
    {
        OpenMenuItem.Click += async (_, _) => await OpenFile();
        ExitMenuItem.Click += (_, _) => Close();

        FullscreenButton.Click += (_, _) => ToggleFullscreen();
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

        // No logic yet — placeholder only
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
        var window = new OptionsWindow();
        window.ShowDialog(this);
    }

    private void AddHotkeys()
    {
        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Space:
                    PlayButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;

                case Key.Left:
                    StepBackButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;

                case Key.Right:
                    StepForwardButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    break;

                case Key.F:
                    ToggleFullscreen();
                    break;
            }
        };
    }
}