using System.ComponentModel;
using Avalonia.Layout;

namespace OMP.Ui.Models;

internal sealed class VerticalAlignmentOption(VerticalAlignment value, string label) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public VerticalAlignment Value { get; } = value;

    public string Label { get; } = label;

    public bool IsSelected
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
