using OMP.Lib.Audio;
using OMP.Ui.Extensions;

namespace OMP.Ui.Tests.Extensions;

public class AudioStreamExtTests
{
    [Fact]
    public void Describe_GivenStream_FormatsLanguageTitleAndCodec()
    {
        var stream = new AudioStream(1, "aac", "Commentary", "en");

        Assert.Equal("[en] Commentary (aac)", stream.Describe());
    }

    [Theory]
    [InlineData(0, "Unknown", "Unknown", "mp3", "Track 1 (mp3)")]
    [InlineData(1, "Unknown", "Unknown", "mp3", "Track 2 (mp3)")]
    [InlineData(0, "Unknown", "und", "aac", "Track 1 (aac)")]
    [InlineData(2, "Unknown", "eng", "aac", "[eng] Track 3 (aac)")]
    [InlineData(0, "Stereo", "Unknown", "aac", "Stereo (aac)")]
    [InlineData(0, "German dub", "de", "Unknown", "[de] German dub")]
    public void Describe_GivenMissingMetadata_LeavesItOut(int id, string title, string language, string codec, string expected)
    {
        var stream = new AudioStream(id, codec, title, language);

        Assert.Equal(expected, stream.Describe());
    }
}
