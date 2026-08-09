using Avalonia.Layout;

namespace OMP.Ui.Models;

internal sealed class VerticalAlignmentOption(VerticalAlignment value, string label)
{
    public VerticalAlignment Value { get; } = value;

    public string Label { get; } = label;
}
