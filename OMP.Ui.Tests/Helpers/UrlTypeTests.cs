using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class UrlTypeTests
{
    [Theory]
    [InlineData("https://www.twitch.tv/videos/123456789")]
    [InlineData("http://example.com")]
    public void IsHttpUrl_HttpOrHttps_ReturnsTrue(string value) =>
        Assert.True(UrlType.IsHttpUrl(value));

    [Theory]
    [InlineData(@"C:\Movies\movie.mkv")]
    [InlineData("/home/user/movie.mp4")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData("ftp://example.com/file")]
    public void IsHttpUrl_LocalPathOrNonHttpScheme_ReturnsFalse(string value) =>
        Assert.False(UrlType.IsHttpUrl(value));
}
