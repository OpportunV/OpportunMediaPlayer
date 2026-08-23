using OMP.Lib.Subtitle;
using OMP.Ui.Extensions;

namespace OMP.Ui.Tests.Extensions;

public class SubtitleStreamExtTests
{
    [Fact]
    public void Describe_TextBasedStream_HasNoUnsupportedSuffix()
    {
        var stream = new SubtitleStream(1, "subrip", "English", "en", IsTextBased: true);

        Assert.Equal("English [en] (subrip)", stream.Describe());
    }

    [Fact]
    public void Describe_NonTextBasedStream_AppendsUnsupportedSuffix()
    {
        var stream = new SubtitleStream(1, "hdmv_pgs_subtitle", "English", "en", IsTextBased: false);

        Assert.Equal("English [en] (hdmv_pgs_subtitle) - unsupported", stream.Describe());
    }
}
