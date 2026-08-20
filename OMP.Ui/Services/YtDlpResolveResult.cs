using System;

namespace OMP.Ui.Services;

public sealed record YtDlpResolveResult
{
    public YtDlpResolveStatus Status { get; }

    public string PageUrl { get; }

    public string? Url { get; }

    public string? Title { get; }

    public string? ErrorMessage { get; }

    private YtDlpResolveResult(YtDlpResolveStatus status, string pageUrl, string? url, string? title, string? errorMessage)
    {
        Status = status;
        PageUrl = pageUrl;
        Url = url;
        Title = title;
        ErrorMessage = errorMessage;
    }

    public static YtDlpResolveResult NotFound(string pageUrl) =>
        new(YtDlpResolveStatus.NotFound, pageUrl, url: null, title: null, errorMessage: null);

    public static YtDlpResolveResult Failed(string pageUrl, string message) =>
        new(YtDlpResolveStatus.Failed, pageUrl, url: null, title: null, message);

    public static YtDlpResolveResult Success(string pageUrl, string url, string? title) =>
        new(YtDlpResolveStatus.Success, pageUrl, url, title, errorMessage: null);

    public TResult Match<TResult>(
        Func<string, string, string?, TResult> onSuccess,
        Func<TResult> onNotFound,
        Func<string, TResult> onFailed) =>
        Status switch
        {
            YtDlpResolveStatus.Success => onSuccess(PageUrl, Url!, Title),
            YtDlpResolveStatus.NotFound => onNotFound(),
            YtDlpResolveStatus.Failed => onFailed(ErrorMessage!),
            _ => throw new ArgumentOutOfRangeException(nameof(Status), Status, "Unhandled yt-dlp resolve status.")
        };
}
