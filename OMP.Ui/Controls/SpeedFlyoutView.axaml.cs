using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OMP.Lib;
using OMP.Ui.Extensions;

namespace OMP.Ui.Controls;

internal sealed partial class SpeedFlyoutView : UserControl
{
    public event Action<double>? SpeedCommitted;

    private readonly List<Button> _presetButtons = [];
    private double _currentSpeed = 1.0;
    private bool _isDragging;

    private const double FineStepAmount = 0.05;
    private const double PresetHighlightEpsilon = 1e-6;

    public SpeedFlyoutView()
    {
        InitializeComponent();

        foreach (var preset in PlaybackSpeedPresets.Values)
        {
            var button = new Button
            {
                Content = PlaybackSpeedFormat.Format(preset),
                Tag = preset,
                Padding = new Thickness(6, 4),
                Margin = new Thickness(1, 2)
            };
            button.Click += (_, _) => SpeedCommitted?.Invoke(preset);
            _presetButtons.Add(button);
            PresetsPanel.Children.Add(button);
        }

        SpeedSlider.Minimum = PlaybackSpeedLimits.Min;
        SpeedSlider.Maximum = PlaybackSpeedLimits.Max;

        DecreaseButton.Click += (_, _)
            => SpeedCommitted?.Invoke(Math.Max(PlaybackSpeedLimits.Min, _currentSpeed - FineStepAmount));
        IncreaseButton.Click += (_, _)
            => SpeedCommitted?.Invoke(Math.Min(PlaybackSpeedLimits.Max, _currentSpeed + FineStepAmount));
        ResetButton.Click += (_, _) => SpeedCommitted?.Invoke(1.0);

        SpeedSlider.AddHandler(PointerPressedEvent, (_, _) => _isDragging = true, RoutingStrategies.Tunnel);
        SpeedSlider.ValueChanged += (_, e) =>
        {
            if (_isDragging)
            {
                SpeedValueLabel.Text = PlaybackSpeedFormat.Format(e.NewValue);
            }
        };
        SpeedSlider.PointerCaptureLost += (_, _) =>
        {
            _isDragging = false;
            SpeedCommitted?.Invoke(SpeedSlider.Value);
        };
    }

    public void SetSpeed(double speed)
    {
        _currentSpeed = speed;

        if (!_isDragging)
        {
            SpeedSlider.Value = speed;
        }

        SpeedValueLabel.Text = PlaybackSpeedFormat.Format(speed);
        HighlightActivePreset(speed);
    }

    private void HighlightActivePreset(double speed)
    {
        foreach (var button in _presetButtons)
        {
            var isActive = Math.Abs((double)button.Tag! - speed) < PresetHighlightEpsilon;
            button.FontWeight = isActive ? FontWeight.Bold : FontWeight.Normal;
            button.BorderBrush = isActive ? Brushes.White : Brushes.Transparent;
            button.BorderThickness = new Thickness(isActive ? 1.5 : 0);
        }
    }
}
