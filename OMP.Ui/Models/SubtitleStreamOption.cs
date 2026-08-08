using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;

namespace OMP.Ui.Models;

internal sealed class SubtitleStreamOption(SubtitleStream stream)
{
    public SubtitleStream Stream { get; } = stream;

    public string Label { get; } = stream.Describe();

    public bool IsSupported { get; } = stream.IsTextBased;
}
