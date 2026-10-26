using OMP.Lib.Interop;

namespace OMP.Lib.Tests;

public class HandlerNameTests
{
    [Theory]
    [InlineData("Stereo", true)]
    [InlineData("Director's commentary", true)]
    [InlineData("SoundHandler", false)]
    [InlineData("VideoHandler", false)]
    [InlineData("GPAC ISO Audio Handler", false)]
    [InlineData("L-SMASH Audio Handler", false)]
    [InlineData("Apple Sound Media Handler", false)]
    [InlineData("Core Media Audio", false)]
    [InlineData("ISO Media file produced by Google Inc.", false)]
    [InlineData("  ", false)]
    [InlineData(null, false)]
    public void IsTrackName_RejectsMuxerDefaults(string? handlerName, bool expected) =>
        Assert.Equal(expected, HandlerName.IsTrackName(handlerName));
}
