using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class MediaFileTypeTests
{
    private static readonly string[] _patterns = ["*.mp4", "*.mkv", "*.mp3"];

    [Theory]
    [InlineData("movie.mp4")]
    [InlineData("movie.MP4")]
    [InlineData("song.mp3")]
    public void IsSupportedMediaFile_MatchingExtension_ReturnsTrue(string path) =>
        Assert.True(MediaFileType.IsSupportedMediaFile(path, _patterns));

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("noextension")]
    [InlineData("")]
    public void IsSupportedMediaFile_NonMatchingExtension_ReturnsFalse(string path) =>
        Assert.False(MediaFileType.IsSupportedMediaFile(path, _patterns));
}
