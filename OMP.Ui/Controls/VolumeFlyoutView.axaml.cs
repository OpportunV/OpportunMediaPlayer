using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using OMP.Lib.Audio.Output;
using OMP.Ui.Models;

namespace OMP.Ui.Controls;

internal sealed partial class VolumeFlyoutView : UserControl
{
    public event Action<AudioOutput, double>? OutputVolumeChanged;

    public event Action<AudioOutput>? OutputVolumeCommitted;

    public event Action<AudioOutput, bool>? OutputMuteChanged;

    private readonly ObservableCollection<OutputVolumeRow> _rows = [];

    public VolumeFlyoutView()
    {
        InitializeComponent();
        OutputsPanel.ItemsSource = _rows;
    }

    public void SetOutputs(IEnumerable<(AudioOutput Output, double Volume, bool Muted)> outputs)
    {
        _rows.Clear();

        var rows = outputs.Select(o => new OutputVolumeRow(o.Output, o.Volume, o.Muted)).ToList();
        if (rows.Count > 0)
        {
            rows[^1].IsLast = true;
        }

        foreach (var row in rows)
        {
            _rows.Add(row);
        }
    }

    private void OnRowVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (((Control)sender!).DataContext is not OutputVolumeRow row)
        {
            return;
        }

        OutputVolumeChanged?.Invoke(row.Output, e.NewValue);
    }

    private void OnRowVolumeReleased(object? sender, PointerCaptureLostEventArgs e)
    {
        if (((Control)sender!).DataContext is not OutputVolumeRow row)
        {
            return;
        }

        OutputVolumeCommitted?.Invoke(row.Output);
    }

    private void OnRowMuteChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { DataContext: OutputVolumeRow row } toggle)
        {
            return;
        }

        OutputMuteChanged?.Invoke(row.Output, toggle.IsChecked == true);
    }
}
