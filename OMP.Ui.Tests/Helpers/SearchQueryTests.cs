using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class SearchQueryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyQuery_MatchesEverything(string? query)
    {
        Assert.True(SearchQuery.Matches(query, "English (auto-generated)"));
    }

    [Theory]
    [InlineData("english")]
    [InlineData("ENGLISH")]
    [InlineData("auto eng")]
    [InlineData("generated")]
    public void EveryWord_MustAppearAnywhereIgnoringCase(string query)
    {
        Assert.True(SearchQuery.Matches(query, "English (auto-generated) [en]"));
    }

    [Fact]
    public void AnyMissingWord_FailsTheMatch()
    {
        Assert.False(SearchQuery.Matches("english translated", "English (auto-generated) [en]"));
    }

    [Fact]
    public void Accents_AreIgnored()
    {
        Assert.True(SearchQuery.Matches("francais", "Français (auto-generated, translated)"));
    }

    [Fact]
    public void NonLatinScripts_MatchCaseInsensitively()
    {
        Assert.True(SearchQuery.Matches("рус", "Русский (автоматические, перевод)"));
    }
}
