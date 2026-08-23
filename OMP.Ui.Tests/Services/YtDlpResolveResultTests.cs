using OMP.Lib.Session;
using OMP.Lib.Subtitle;
using OMP.Ui.Services;

namespace OMP.Ui.Tests.Services;

public class YtDlpResolveResultTests
{
    [Fact]
    public void Success_Match_InvokesOnSuccessWithPageUrlAndTitle()
    {
        var result = YtDlpResolveResult.Success("https://example.com/watch", "https://example.com/media.mp4", "Title");

        var matched = result.Match(
            onSuccess: (pageUrl, url, title, _, _) => $"{pageUrl}|{url}|{title}",
            onNotFound: () => "not-found",
            onFailed: message => $"failed:{message}");

        Assert.Equal("https://example.com/watch|https://example.com/media.mp4|Title", matched);
    }

    [Fact]
    public void Success_WithAudioSidecars_ThreadsSidecarsThroughMatch()
    {
        IReadOnlyList<AudioSidecarSource> sidecars = [new("https://example.com/fr.m4a", "fr", "French")];
        var result = YtDlpResolveResult.Success(
            "https://example.com/watch", "https://example.com/media.mp4", "Title", sidecars);

        var matched = result.Match(
            onSuccess: (_, _, _, audioSidecars, _) => audioSidecars.Count,
            onNotFound: () => -1,
            onFailed: _ => -1);

        Assert.Equal(1, matched);
    }

    [Fact]
    public void Success_WithSubtitleSidecars_ThreadsSidecarsThroughMatch()
    {
        IReadOnlyList<SubtitleSidecarSource> sidecars = [new("https://example.com/en.vtt", "en", "English")];
        var result = YtDlpResolveResult.Success(
            "https://example.com/watch", "https://example.com/media.mp4", "Title", subtitleSidecars: sidecars);

        var matched = result.Match(
            onSuccess: (_, _, _, _, subtitleSidecars) => subtitleSidecars.Count,
            onNotFound: () => -1,
            onFailed: _ => -1);

        Assert.Equal(1, matched);
    }

    [Fact]
    public void NotFound_Match_InvokesOnNotFound()
    {
        var result = YtDlpResolveResult.NotFound("https://example.com/watch");

        var matched = result.Match(
            onSuccess: (_, _, _, _, _) => "success",
            onNotFound: () => "not-found",
            onFailed: _ => "failed");

        Assert.Equal("not-found", matched);
    }

    [Fact]
    public void Failed_Match_InvokesOnFailedWithMessage()
    {
        var result = YtDlpResolveResult.Failed("https://example.com/watch", "no playable format");

        var matched = result.Match(
            onSuccess: (_, _, _, _, _) => "success",
            onNotFound: () => "not-found",
            onFailed: message => message);

        Assert.Equal("no playable format", matched);
    }
}
