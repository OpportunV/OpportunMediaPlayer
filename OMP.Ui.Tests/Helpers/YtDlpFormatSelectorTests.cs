using System.Text.Json;
using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class YtDlpFormatSelectorTests
{
    [Fact]
    public void SelectMediaSources_TopLevelProgressive_ReturnsPrimaryUrlAndTitleNoSidecars()
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

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/progressive.mp4", result.Url);
        Assert.Equal("Sample Video", result.Title);
        Assert.Empty(result.AudioSidecars);
    }

    [Fact]
    public void SelectMediaSources_ProgressiveInFormatsArray_PicksHighestQuality()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "title": "Adaptive Video",
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "avc1.42001E", "acodec": "mp4a.40.2", "height": 360, "url": "https://example.com/360p.mp4" },
                    { "vcodec": "avc1.4d401f", "acodec": "mp4a.40.2", "height": 720, "url": "https://example.com/720p.mp4" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/720p.mp4", result.Url);
    }

    [Fact]
    public void SelectMediaSources_NoProgressiveAnywhere_VideoOnlyPrimaryPlusAudioSidecar()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "title": "Split Streams Only",
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "none", "acodec": "mp4a.40.2", "language": "en", "url": "https://example.com/audio-only.m4a" },
                    { "vcodec": "avc1.4d401e", "acodec": "none", "url": "https://example.com/video-only.mp4" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/video-only.mp4", result.Url);
        var sidecar = Assert.Single(result.AudioSidecars);
        Assert.Equal("https://example.com/audio-only.m4a", sidecar.Url);
        Assert.Equal("en", sidecar.Language);
    }

    [Fact]
    public void SelectMediaSources_PureAudioOnlySource_UsesAudioOnlyPrimaryNoVideoAnywhere()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "title": "Podcast Episode",
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "none", "acodec": "mp4a.40.2", "tbr": 128, "url": "https://example.com/audio-low.m4a" },
                    { "vcodec": "none", "acodec": "mp4a.40.2", "tbr": 256, "url": "https://example.com/audio-high.m4a" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/audio-high.m4a", result.Url);
        Assert.Empty(result.AudioSidecars);
    }

    [Fact]
    public void SelectMediaSources_ProgressivePrimaryWithAdditionalAudioOnlyAlternateLanguage_SidecarStillPickedUp()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "title": "Dubbed Video",
                "vcodec": "avc1.64001F",
                "acodec": "mp4a.40.2",
                "url": "https://example.com/progressive.mp4",
                "formats": [
                    { "vcodec": "avc1.64001F", "acodec": "mp4a.40.2", "url": "https://example.com/progressive.mp4" },
                    { "vcodec": "none", "acodec": "mp4a.40.2", "language": "fr", "url": "https://example.com/audio-fr.m4a" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/progressive.mp4", result.Url);
        var sidecar = Assert.Single(result.AudioSidecars);
        Assert.Equal("https://example.com/audio-fr.m4a", sidecar.Url);
        Assert.Equal("fr", sidecar.Language);
    }

    [Fact]
    public void SelectMediaSources_TwoAudioOnlyFormatsSameLanguage_OnlyBetterScoredSurvives()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "avc1.4d401e", "acodec": "none", "url": "https://example.com/video-only.mp4" },
                    { "vcodec": "none", "acodec": "mp4a.40.2", "language": "en", "tbr": 64, "url": "https://example.com/audio-en-low.m4a" },
                    { "vcodec": "none", "acodec": "mp4a.40.2", "language": "en", "tbr": 128, "url": "https://example.com/audio-en-high.m4a" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        var sidecar = Assert.Single(result.AudioSidecars);
        Assert.Equal("https://example.com/audio-en-high.m4a", sidecar.Url);
    }

    [Fact]
    public void SelectMediaSources_AudioOnlyFormatWithNoLanguage_StillIncludedWithNullLanguageAndTitle()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "avc1.4d401e", "acodec": "none", "url": "https://example.com/video-only.mp4" },
                    { "vcodec": "none", "acodec": "mp4a.40.2", "url": "https://example.com/audio-unlabeled.m4a" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        var sidecar = Assert.Single(result.AudioSidecars);
        Assert.Equal("https://example.com/audio-unlabeled.m4a", sidecar.Url);
        Assert.Null(sidecar.Language);
        Assert.Null(sidecar.Title);
    }

    [Fact]
    public void SelectMediaSources_OriginalAndDubbedSidecars_OriginalSortsFirst()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "avc1.4d401e", "acodec": "none", "url": "https://example.com/video-only.mp4" },
                    { "vcodec": "none", "acodec": "mp4a.40.2", "language": "de", "language_preference": -1, "url": "https://example.com/audio-de.m4a" },
                    { "vcodec": "none", "acodec": "mp4a.40.2", "language": "en", "language_preference": 10, "url": "https://example.com/audio-en.m4a" },
                    { "vcodec": "none", "acodec": "mp4a.40.2", "language": "fr", "language_preference": -1, "url": "https://example.com/audio-fr.m4a" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Equal(3, result.AudioSidecars.Count);
        Assert.Equal("en", result.AudioSidecars[0].Language);
    }

    [Fact]
    public void SelectMediaSources_NoPlayableFormatAnywhere_ReturnsNull()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "title": "Empty",
                "vcodec": "none",
                "acodec": "none",
                "formats": []
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.Null(result);
    }

    [Fact]
    public void SelectMediaSources_PrimaryHasHttpHeaders_HeadersAreCaptured()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "avc1.64001F",
                "acodec": "mp4a.40.2",
                "url": "https://googlevideo.com/progressive.mp4",
                "http_headers": {
                    "User-Agent": "Mozilla/5.0",
                    "Referer": "https://www.youtube.com/"
                }
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.NotNull(result.Headers);
        Assert.Equal("Mozilla/5.0", result.Headers!["User-Agent"]);
        Assert.Equal("https://www.youtube.com/", result.Headers!["Referer"]);
    }

    [Fact]
    public void SelectMediaSources_NoHttpHeaders_HeadersAreNull()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "avc1.64001F",
                "acodec": "mp4a.40.2",
                "url": "https://example.com/progressive.mp4"
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Null(result.Headers);
    }

    [Fact]
    public void SelectMediaSources_AudioSidecarHasHttpHeaders_HeadersAreCaptured()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    {
                        "vcodec": "none",
                        "acodec": "mp4a.40.2",
                        "language": "en",
                        "url": "https://example.com/audio-only.m4a",
                        "http_headers": { "User-Agent": "Mozilla/5.0" }
                    },
                    { "vcodec": "avc1.4d401e", "acodec": "none", "url": "https://example.com/video-only.mp4" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        var sidecar = Assert.Single(result.AudioSidecars);
        Assert.NotNull(sidecar.Headers);
        Assert.Equal("Mozilla/5.0", sidecar.Headers!["User-Agent"]);
    }

    [Fact]
    public void SelectMediaSources_HighestFormatAboveCap_PicksLowerFormatWithinCap()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "avc1.4d401f", "acodec": "none", "height": 1080, "url": "https://example.com/1080p.mp4" },
                    { "vcodec": "avc1.640033", "acodec": "none", "height": 1440, "url": "https://example.com/1440p.mp4" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/1080p.mp4", result.Url);
    }

    [Fact]
    public void SelectMediaSources_NoFormatWithinCap_FallsBackToHighestAvailable()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "none",
                "acodec": "none",
                "formats": [
                    { "vcodec": "avc1.640028", "acodec": "none", "height": 1440, "url": "https://example.com/1440p.mp4" },
                    { "vcodec": "avc1.640033", "acodec": "none", "height": 2160, "url": "https://example.com/2160p.mp4" }
                ]
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Equal("https://example.com/2160p.mp4", result.Url);
    }

    [Fact]
    public void SelectMediaSources_TitleMissing_ReturnsNullTitle()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "vcodec": "avc1.64001F",
                "acodec": "mp4a.40.2",
                "url": "https://example.com/progressive.mp4"
            }
            """);

        var result = YtDlpFormatSelector.SelectMediaSources(document);

        Assert.NotNull(result);
        Assert.Null(result.Title);
    }
}
