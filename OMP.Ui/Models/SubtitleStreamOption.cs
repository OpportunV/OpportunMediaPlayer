using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;
using OMP.Ui.Helpers;

namespace OMP.Ui.Models;

internal sealed class SubtitleStreamOption(SubtitleStream stream)
{
    public SubtitleStream Stream { get; } = stream;

    public string Label { get; } = stream.Describe();

    public bool IsSupported { get; } = stream.IsTextBased;

    public string SearchText { get; } = string.Join(
        ' ',
        stream.Describe(),
        stream.Language,
        LanguageDisplay.EnglishName(stream.Language),
        LanguageDisplay.NativeName(stream.Language));
}
