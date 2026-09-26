using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;
using OMP.Ui.Helpers;

namespace OMP.Ui.Models;

internal sealed class SubtitleStreamOption(SubtitleStream stream)
{
    public SubtitleStream Stream { get; } = stream;

    public string Label { get; } = stream.Describe();

    public bool IsSupported { get; } = stream.IsTextBased;

    /// <summary>
    /// Everything a search can match against: the label as shown, plus the language code and its
    /// English and native names, since a web video's labels come in whatever language YouTube used.
    /// </summary>
    public string SearchText { get; } = string.Join(
        ' ',
        stream.Describe(),
        stream.Language,
        LanguageDisplay.EnglishName(stream.Language),
        LanguageDisplay.NativeName(stream.Language));
}
