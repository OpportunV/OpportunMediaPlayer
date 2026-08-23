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
}
