using System.ComponentModel;
using Avalonia.Layout;

namespace OMP.Ui.Models;

internal sealed class HorizontalAlignmentOption(HorizontalAlignment value, string label)
    : INotifyPropertyChanged, IAlignmentOption<HorizontalAlignment>
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public HorizontalAlignment Value { get; } = value;

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
