using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OMP.Lib.Session;
using OMP.Ui.Extensions;

namespace OMP.Ui.Services;

/// <summary>
/// Drives everything that has to follow playback position: the progress slider, the elapsed-time
/// readout, and the subtitle overlay. Also owns seeking by slider and the subtitles toggle, since
/// all three share the state the tick reads.
/// </summary>
internal sealed class PlaybackStatusTimer : IDisposable
{
    private const int TickIntervalMs = 200;

    private readonly DispatcherTimer _timer = new();
    private readonly IMediaSessionRegistry _mediaSessionRegistry;
    private readonly Slider _progressSlider;
    private readonly TextBlock _currentTimeLabel;
    private readonly ToggleButton _subtitlesButton;
    private readonly Action<IMediaSession> _renderSubtitleOverlay;
    private readonly Action _clearSubtitleOverlay;

    private bool _isSeekingViaSlider;
    private bool _areSubtitlesEnabled;
    private int _lastKnownSubtitleRouteCount;

    public PlaybackStatusTimer(
        IMediaSessionRegistry mediaSessionRegistry,
        Slider progressSlider,
        TextBlock currentTimeLabel,
        ToggleButton subtitlesButton,
        Action<IMediaSession> renderSubtitleOverlay,
        Action clearSubtitleOverlay)
    {
        _mediaSessionRegistry = mediaSessionRegistry;
        _progressSlider = progressSlider;
        _currentTimeLabel = currentTimeLabel;
        _subtitlesButton = subtitlesButton;
        _renderSubtitleOverlay = renderSubtitleOverlay;
        _clearSubtitleOverlay = clearSubtitleOverlay;

        _subtitlesButton.IsChecked = _areSubtitlesEnabled;
        _subtitlesButton.IsCheckedChanged += OnSubtitlesToggled;

        SetupProgressSlider();

        _timer.Interval = TimeSpan.FromMilliseconds(TickIntervalMs);
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();

    public void ResetForNewSession()
    {
        _areSubtitlesEnabled = false;
        _lastKnownSubtitleRouteCount = 0;
        _subtitlesButton.IsChecked = false;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _subtitlesButton.IsCheckedChanged -= OnSubtitlesToggled;
    }

    internal void Tick()
    {
        var session = _mediaSessionRegistry.Current;

        if (session == null)
        {
            return;
        }

        if (!_isSeekingViaSlider)
        {
            _progressSlider.Value = session.CurrentTime.TotalSeconds;
        }

        _currentTimeLabel.Text = session.CurrentTime.Format();

        var subtitleRouteCount = session.SubtitleRoutes.Count;
        if (subtitleRouteCount > 0 && _lastKnownSubtitleRouteCount == 0)
        {
            _areSubtitlesEnabled = true;
            _subtitlesButton.IsChecked = true;
        }

        _lastKnownSubtitleRouteCount = subtitleRouteCount;

        if (_areSubtitlesEnabled)
        {
            _renderSubtitleOverlay(session);
        }
    }

    private void OnTick(object? sender, EventArgs e) => Tick();

    private void OnSubtitlesToggled(object? sender, RoutedEventArgs e)
    {
        _areSubtitlesEnabled = _subtitlesButton.IsChecked == true;

        if (!_areSubtitlesEnabled)
        {
            _clearSubtitleOverlay();
        }
    }

    private void SetupProgressSlider()
    {
        _progressSlider.PointerMoved += (_, e) =>
        {
            if (_mediaSessionRegistry.Current == null || _progressSlider.Bounds.Width <= 0)
            {
                return;
            }

            var ratio = Math.Clamp(e.GetPosition(_progressSlider).X / _progressSlider.Bounds.Width, 0, 1);
            var hoveredTime = TimeSpan.FromSeconds(ratio * _progressSlider.Maximum);
            ToolTip.SetTip(_progressSlider, hoveredTime.Format());
        };

        _progressSlider.AddHandler(
            InputElement.PointerPressedEvent, (_, _) => _isSeekingViaSlider = true, RoutingStrategies.Tunnel);

        _progressSlider.PointerCaptureLost += (_, _) =>
        {
            _mediaSessionRegistry.Current?.Seek(TimeSpan.FromSeconds(_progressSlider.Value));
            _isSeekingViaSlider = false;
        };
    }
}
