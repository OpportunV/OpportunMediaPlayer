using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using OMP.Lib;
using OMP.Ui.Helpers;

namespace OMP.Ui.Controls;

internal sealed partial class SpeedFlyoutView : UserControl
{
    public event Action<double>? SpeedCommitted;

    private readonly List<ToggleButton> _presetButtons = [];
    private double _currentSpeed = 1.0;
    private bool _isDragging;

    private const double FineStepAmount = 0.05;
    private const double PresetHighlightEpsilon = 1e-6;

    public SpeedFlyoutView()
    {
        InitializeComponent();

        foreach (var preset in PlaybackSpeedPresets.Values)
        {
            var button = new ToggleButton
            {
                Classes = { "seg" },
                Content = PlaybackSpeedFormat.Format(preset),
                Tag = preset,
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

        SpeedSlider.AddHandler(PointerPressedEvent, (_, _) => _isDragging = true, RoutingStrategies.Tunnel);
        SpeedSlider.ValueChanged += (_, e) =>
        {
            if (_isDragging)
            {
                SpeedValueLabel.Text = FormatFineValue(e.NewValue);
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

        SpeedValueLabel.Text = FormatFineValue(speed);
        HighlightActivePreset(speed);
    }

    private void HighlightActivePreset(double speed)
    {
        var matchedPreset = false;

        foreach (var button in _presetButtons)
        {
            var isActive = Math.Abs((double)button.Tag! - speed) < PresetHighlightEpsilon;
            button.IsChecked = isActive;
            matchedPreset |= isActive;
        }

        if (matchedPreset)
        {
            SpeedValueLabel.ClearValue(TextBlock.ForegroundProperty);
        }
        else if (Application.Current!.TryGetResource("AccentTextBrush", ActualThemeVariant, out var resource) &&
                 resource is IBrush brush)
        {
            SpeedValueLabel.Foreground = brush;
        }
    }

    private static string FormatFineValue(double speed) => speed.ToString("0.00", CultureInfo.InvariantCulture) + "×";
}
