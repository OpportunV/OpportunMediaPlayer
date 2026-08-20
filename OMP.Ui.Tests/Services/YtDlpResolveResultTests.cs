using OMP.Ui.Services;

namespace OMP.Ui.Tests.Services;

public class YtDlpResolveResultTests
{
    [Fact]
    public void Success_Match_InvokesOnSuccessWithPageUrlAndTitle()
    {
        var result = YtDlpResolveResult.Success("https://example.com/watch", "https://example.com/media.mp4", "Title");

        var matched = result.Match(
            onSuccess: (pageUrl, url, title) => $"{pageUrl}|{url}|{title}",
            onNotFound: () => "not-found",
            onFailed: message => $"failed:{message}");

        Assert.Equal("https://example.com/watch|https://example.com/media.mp4|Title", matched);
    }

    [Fact]
    public void NotFound_Match_InvokesOnNotFound()
    {
        var result = YtDlpResolveResult.NotFound("https://example.com/watch");

        var matched = result.Match(
            onSuccess: (_, _, _) => "success",
            onNotFound: () => "not-found",
            onFailed: _ => "failed");

        Assert.Equal("not-found", matched);
    }

    [Fact]
    public void Failed_Match_InvokesOnFailedWithMessage()
    {
        var result = YtDlpResolveResult.Failed("https://example.com/watch", "no playable format");

        var matched = result.Match(
            onSuccess: (_, _, _) => "success",
            onNotFound: () => "not-found",
            onFailed: message => message);

        Assert.Equal("no playable format", matched);
    }
}
