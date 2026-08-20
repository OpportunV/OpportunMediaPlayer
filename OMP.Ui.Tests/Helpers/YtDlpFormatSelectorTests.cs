using System.Text.Json;
using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class YtDlpFormatSelectorTests
{
    [Fact]
    public void SelectPlayableFormat_TopLevelProgressive_ReturnsTopLevelUrlAndTitle()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "title": "Sample Video",
                "vcodec": "avc1.64001F",
                "acodec": "mp4a.40.2",
                "url": "https://example.com/progressive.mp4"
            }
            """);

        var result = YtDlpFormatSelector.SelectPlayableFormat(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/progressive.mp4", result.Value.Url);
        Assert.Equal("Sample Video", result.Value.Title);
    }

    [Fact]
    public void SelectPlayableFormat_AdaptiveTopLevel_ScansFormatsForHighestQualityProgressive()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "title": "Adaptive Video",
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "none", "acodec": "mp4a.40.2", "url": "https://example.com/audio-only.m4a" },
                    { "vcodec": "avc1.4d401e", "acodec": "none", "url": "https://example.com/video-only.mp4" },
                    { "vcodec": "avc1.42001E", "acodec": "mp4a.40.2", "height": 360, "url": "https://example.com/360p.mp4" },
                    { "vcodec": "avc1.4d401f", "acodec": "mp4a.40.2", "height": 720, "url": "https://example.com/720p.mp4" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectPlayableFormat(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/720p.mp4", result.Value.Url);
    }

    [Fact]
    public void SelectPlayableFormat_NoProgressiveFormatAnywhere_ReturnsNull()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "title": "Split Streams Only",
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "none", "acodec": "mp4a.40.2", "url": "https://example.com/audio-only.m4a" },
                    { "vcodec": "avc1.4d401e", "acodec": "none", "url": "https://example.com/video-only.mp4" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectPlayableFormat(document);

        Assert.Null(result);
    }

    [Fact]
    public void SelectPlayableFormat_TitleMissing_ReturnsNullTitle()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "avc1.64001F",
                "acodec": "mp4a.40.2",
                "url": "https://example.com/progressive.mp4"
            }
            """);

        var result = YtDlpFormatSelector.SelectPlayableFormat(document);

        Assert.NotNull(result);
        Assert.Null(result.Value.Title);
    }
}
