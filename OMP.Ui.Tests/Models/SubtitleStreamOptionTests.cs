using OMP.Lib.Subtitle;
using OMP.Ui.Helpers;
using OMP.Ui.Models;

namespace OMP.Ui.Tests.Models;

public class SubtitleStreamOptionTests
{
    [Fact]
    public void SearchText_FindsANativeNamedTrackByItsEnglishName()
    {
        var option = new SubtitleStreamOption(new SubtitleStream(1, "Unknown", "Русский (auto-generated, translated)", "ru", IsTextBased: true));

        Assert.True(SearchQuery.Matches("russian", option.SearchText));
    }

    [Fact]
    public void SearchText_FindsATrackByItsLanguageCode()
    {
        var option = new SubtitleStreamOption(new SubtitleStream(1, "subrip", "Subs", "de", IsTextBased: true));

        Assert.True(SearchQuery.Matches("german", option.SearchText));
        Assert.True(SearchQuery.Matches("deutsch", option.SearchText));
    }

    [Fact]
    public void SearchText_UnknownLanguageCode_StillSearchable()
    {
        var option = new SubtitleStreamOption(new SubtitleStream(1, "subrip", "Commentary", "zz-unknown", IsTextBased: true));

        Assert.True(SearchQuery.Matches("commentary", option.SearchText));
    }
}
