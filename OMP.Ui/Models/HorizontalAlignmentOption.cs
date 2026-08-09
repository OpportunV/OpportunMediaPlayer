using Avalonia.Layout;

namespace OMP.Ui.Models;

internal sealed class HorizontalAlignmentOption(HorizontalAlignment value, string label)
{
    public HorizontalAlignment Value { get; } = value;

    public string Label { get; } = label;
}
