using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using OMP.Ui.Extensions;
using OMP.Ui.Helpers;
using OMP.Ui.Localization;
using OMP.Ui.Models;
using OMP.Ui.Settings;

namespace OMP.Ui.Windows;

public sealed partial class SubtitleZoneEditorWindow : Window
{
    private readonly List<AspectRatioOption> _aspectRatios =
    [
        new("16:9", 16.0 / 9.0),
        new("4:3", 4.0 / 3.0),
        new("21:9", 21.0 / 9.0),
        new("1:1", 1.0)
    ];

    private readonly List<HorizontalAlignmentOption> _horizontalAlignOptions;
    private readonly List<VerticalAlignmentOption> _verticalAlignOptions;

    private SubtitleZone _zone = new();
    private bool _isDraggingZone;
    private bool _isResizingZone;
    private bool _isUpdatingHorizontalAlign;
    private bool _isUpdatingVerticalAlign;
    private Point _dragStartPointerPosition;
    private double _dragStartLeft;
    private double _dragStartTop;
    private double _dragStartWidth;
    private double _dragStartHeight;
    private double _videoLeft;
    private double _videoTop;
    private double _videoWidth;
    private double _videoHeight;

    private const double CanvasWidth = 480;
    private const double MinZoneSizePx = 24;
    private const double ResizeHandleSize = 12;

    public SubtitleZoneEditorWindow()
    {
        InitializeComponent();

        SampleText.Inlines =
        [
            new Run(Strings.SubtitleZoneEditor_SamplePrefix),
            new LineBreak(),
            new Run(Strings.SubtitleZoneEditor_SampleWith),
            new Run(Strings.SubtitleZoneEditor_SampleBold) { FontWeight = FontWeight.Bold },
            new Run(Strings.SubtitleZoneEditor_SampleAnd),
            new Run(Strings.SubtitleZoneEditor_SampleItalic) { FontStyle = FontStyle.Italic },
            new Run(Strings.SubtitleZoneEditor_SampleStyles)
        ];

        ScreenAspectRatioSelector.ItemsSource = _aspectRatios;
        ScreenAspectRatioSelector.SelectedIndex = 0;
        VideoAspectRatioSelector.ItemsSource = _aspectRatios;
        VideoAspectRatioSelector.SelectedIndex = 0;

        _horizontalAlignOptions = new[]
        {
            HorizontalAlignment.Left, HorizontalAlignment.Center, HorizontalAlignment.Right
        }.Select(value => new HorizontalAlignmentOption(value, value.ToDisplayLabel())).ToList();
        HorizontalAlignSelector.ItemsSource = _horizontalAlignOptions;

        _verticalAlignOptions = new[]
        {
            VerticalAlignment.Top, VerticalAlignment.Center, VerticalAlignment.Bottom
        }.Select(value => new VerticalAlignmentOption(value, value.ToDisplayLabel())).ToList();
        VerticalAlignSelector.ItemsSource = _verticalAlignOptions;

        FontFamilySelector.ItemsSource = FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        ScreenAspectRatioSelector.SelectionChanged += (_, _) => RebuildCanvas();
        VideoAspectRatioSelector.SelectionChanged += (_, _) => RebuildCanvas();
        NameTextBox.TextChanged += (_, _) => _zone.Name = NameTextBox.Text ?? string.Empty;
        FontFamilySelector.SelectionChanged += (_, _) =>
        {
            _zone.FontFamily = FontFamilySelector.SelectedItem as string ?? _zone.FontFamily;
            UpdateSampleTextStyle();
        };
        FontSizeSlider.ValueChanged += (_, e) =>
        {
            _zone.FontSizeRatio = e.NewValue / 100;
            FontSizeValueText.Text = $"{e.NewValue:0.#}%";
            UpdateSampleTextStyle();
        };
        FontColorPicker.ColorChanged += (_, e) =>
        {
            _zone.FontColor = ToHex(e.NewColor);
            UpdateSampleTextStyle();
        };
        BackgroundColorPicker.ColorChanged += (_, e) =>
        {
            _zone.BackgroundColor = ToHex(e.NewColor);
            UpdateSampleTextStyle();
        };
        BackgroundOpacitySlider.ValueChanged += (_, e) =>
        {
            _zone.BackgroundOpacity = e.NewValue / 100;
            BackgroundOpacityValueText.Text = $"{e.NewValue:0}%";
            UpdateSampleTextStyle();
        };

        SetupZoneDragging();
        SetupZoneResizing();

        CancelButton.Click += (_, _) => Close(null);
        SaveButton.Click += (_, _) => Close(_zone);
    }

    public void Load(SubtitleZone zone, bool isNew)
    {
        _zone = zone;
        Title = isNew ? Strings.SubtitleZoneEditor_NewTitle : string.Format(Strings.SubtitleZoneEditor_EditTitleFormat, zone.Name);

        NameTextBox.Text = zone.Name;
        FontFamilySelector.SelectedItem = zone.FontFamily;
        FontSizeSlider.Value = zone.FontSizeRatio * 100;
        FontSizeValueText.Text = $"{FontSizeSlider.Value:0.#}%";
        FontColorPicker.Color = Color.Parse(zone.FontColor);
        BackgroundColorPicker.Color = Color.Parse(zone.BackgroundColor);
        BackgroundOpacitySlider.Value = zone.BackgroundOpacity * 100;
        BackgroundOpacityValueText.Text = $"{BackgroundOpacitySlider.Value:0}%";

        foreach (var option in _horizontalAlignOptions)
        {
            option.IsSelected = option.Value == zone.HorizontalAlignment;
        }

        foreach (var option in _verticalAlignOptions)
        {
            option.IsSelected = option.Value == zone.VerticalAlignment;
        }

        RebuildCanvas();
    }

    private void SetupZoneDragging()
    {
        ZoneBorder.PointerPressed += (_, e) =>
        {
            _isDraggingZone = true;
            _dragStartPointerPosition = e.GetPosition(PreviewCanvas);
            _dragStartLeft = Canvas.GetLeft(ZoneBorder);
            _dragStartTop = Canvas.GetTop(ZoneBorder);
            e.Pointer.Capture(ZoneBorder);
        };
        ZoneBorder.PointerMoved += (_, e) =>
        {
            if (!_isDraggingZone)
            {
                return;
            }

            var position = e.GetPosition(PreviewCanvas);
            var (newLeft, newTop) = SubtitleZoneGeometry.ClampPosition(
                _dragStartLeft + (position.X - _dragStartPointerPosition.X),
                _dragStartTop + (position.Y - _dragStartPointerPosition.Y),
                ZoneBorder.Width,
                ZoneBorder.Height,
                CanvasWidth,
                PreviewCanvas.Height);

            Canvas.SetLeft(ZoneBorder, newLeft);
            Canvas.SetTop(ZoneBorder, newTop);
            _zone.X = newLeft / CanvasWidth;
            _zone.Y = newTop / PreviewCanvas.Height;
            UpdateResizeHandlePosition();
        };
        ZoneBorder.PointerReleased += (_, e) =>
        {
            _isDraggingZone = false;
            e.Pointer.Capture(null);
        };
    }

    private void SetupZoneResizing()
    {
        ResizeHandle.PointerPressed += (_, e) =>
        {
            _isResizingZone = true;
            _dragStartPointerPosition = e.GetPosition(PreviewCanvas);
            _dragStartWidth = ZoneBorder.Width;
            _dragStartHeight = ZoneBorder.Height;
            e.Pointer.Capture(ResizeHandle);
            e.Handled = true;
        };
        ResizeHandle.PointerMoved += (_, e) =>
        {
            if (!_isResizingZone)
            {
                return;
            }

            var position = e.GetPosition(PreviewCanvas);
            var left = Canvas.GetLeft(ZoneBorder);
            var top = Canvas.GetTop(ZoneBorder);
            var (newWidth, newHeight) = SubtitleZoneGeometry.ClampSize(
                _dragStartWidth + (position.X - _dragStartPointerPosition.X),
                _dragStartHeight + (position.Y - _dragStartPointerPosition.Y),
                MinZoneSizePx,
                left,
                top,
                CanvasWidth,
                PreviewCanvas.Height);

            ZoneBorder.Width = newWidth;
            ZoneBorder.Height = newHeight;
            _zone.Width = newWidth / CanvasWidth;
            _zone.Height = newHeight / PreviewCanvas.Height;
            UpdateResizeHandlePosition();
        };
        ResizeHandle.PointerReleased += (_, e) =>
        {
            _isResizingZone = false;
            e.Pointer.Capture(null);
        };
    }

    private void OnHorizontalAlignChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingHorizontalAlign ||
            sender is not ToggleButton { DataContext: HorizontalAlignmentOption option } button)
        {
            return;
        }

        if (button.IsChecked != true)
        {
            _isUpdatingHorizontalAlign = true;
            button.IsChecked = true;
            _isUpdatingHorizontalAlign = false;
            return;
        }

        _isUpdatingHorizontalAlign = true;
        foreach (var other in _horizontalAlignOptions.Where(o => !ReferenceEquals(o, option)))
        {
            other.IsSelected = false;
        }
        _isUpdatingHorizontalAlign = false;

        _zone.HorizontalAlignment = option.Value;
        UpdateSampleTextStyle();
    }

    private void OnVerticalAlignChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingVerticalAlign ||
            sender is not ToggleButton { DataContext: VerticalAlignmentOption option } button)
        {
            return;
        }

        if (button.IsChecked != true)
        {
            _isUpdatingVerticalAlign = true;
            button.IsChecked = true;
            _isUpdatingVerticalAlign = false;
            return;
        }

        _isUpdatingVerticalAlign = true;
        foreach (var other in _verticalAlignOptions.Where(o => !ReferenceEquals(o, option)))
        {
            other.IsSelected = false;
        }
        _isUpdatingVerticalAlign = false;

        _zone.VerticalAlignment = option.Value;
        UpdateSampleTextStyle();
    }

    private void RebuildCanvas()
    {
        var screenRatio = ((AspectRatioOption)ScreenAspectRatioSelector.SelectedItem!).Ratio;
        var videoRatio = ((AspectRatioOption)VideoAspectRatioSelector.SelectedItem!).Ratio;

        var screenHeight = CanvasWidth / screenRatio;
        PreviewCanvas.Height = screenHeight;

        if (videoRatio > screenRatio)
        {
            _videoWidth = CanvasWidth;
            _videoHeight = CanvasWidth / videoRatio;
        }
        else
        {
            _videoHeight = screenHeight;
            _videoWidth = screenHeight * videoRatio;
        }

        _videoLeft = (CanvasWidth - _videoWidth) / 2;
        _videoTop = (screenHeight - _videoHeight) / 2;

        Canvas.SetLeft(VideoAreaBorder, _videoLeft);
        Canvas.SetTop(VideoAreaBorder, _videoTop);
        VideoAreaBorder.Width = _videoWidth;
        VideoAreaBorder.Height = _videoHeight;

        Canvas.SetLeft(ZoneBorder, _zone.X * CanvasWidth);
        Canvas.SetTop(ZoneBorder, _zone.Y * screenHeight);
        ZoneBorder.Width = _zone.Width * CanvasWidth;
        ZoneBorder.Height = _zone.Height * screenHeight;

        UpdateResizeHandlePosition();
        UpdateSampleTextStyle();
    }

    private void UpdateResizeHandlePosition()
    {
        Canvas.SetLeft(ResizeHandle, Canvas.GetLeft(ZoneBorder) + ZoneBorder.Width - ResizeHandleSize / 2);
        Canvas.SetTop(ResizeHandle, Canvas.GetTop(ZoneBorder) + ZoneBorder.Height - ResizeHandleSize / 2);
    }

    private void UpdateSampleTextStyle()
    {
        SampleText.FontFamily = new FontFamily(_zone.FontFamily);
        SampleText.FontSize = Math.Max(6, _zone.FontSizeRatio * PreviewCanvas.Height);
        SampleText.Foreground = new SolidColorBrush(Color.Parse(_zone.FontColor));
        SampleText.Background = new SolidColorBrush(Color.Parse(_zone.BackgroundColor), _zone.BackgroundOpacity);
        SampleText.TextAlignment = _zone.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => TextAlignment.Left,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Center
        };
        SampleText.HorizontalAlignment = _zone.HorizontalAlignment;
        SampleText.VerticalAlignment = _zone.VerticalAlignment;
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private sealed record AspectRatioOption(string Label, double Ratio)
    {
        public override string ToString() => Label;
    }
}
