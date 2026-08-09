namespace OMP.Ui.Models;

internal sealed class LanguageOption(string? cultureCode, string label)
{
    public string? CultureCode { get; } = cultureCode;

    public string Label { get; } = label;
}
