using System;
using System.Collections.Generic;
using OMP.Lib.Session;

namespace OMP.Ui.Services;

public sealed record YtDlpResolveResult
{
    public YtDlpResolveStatus Status { get; }

    public string PageUrl { get; }

    public string? Url { get; }

    public string? Title { get; }

    public string? ErrorMessage { get; }

    public IReadOnlyList<AudioSidecarSource> AudioSidecars { get; }

    public IReadOnlyDictionary<string, string>? Headers { get; }

    private YtDlpResolveResult(
        YtDlpResolveStatus status,
        string pageUrl,
        string? url,
        string? title,
        string? errorMessage,
        IReadOnlyList<AudioSidecarSource> audioSidecars,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        Status = status;
        PageUrl = pageUrl;
        Url = url;
        Title = title;
        ErrorMessage = errorMessage;
        AudioSidecars = audioSidecars;
        Headers = headers;
    }

    public static YtDlpResolveResult NotFound(string pageUrl) =>
        new(YtDlpResolveStatus.NotFound, pageUrl, url: null, title: null, errorMessage: null, audioSidecars: []);

    public static YtDlpResolveResult Failed(string pageUrl, string message) =>
        new(YtDlpResolveStatus.Failed, pageUrl, url: null, title: null, message, audioSidecars: []);

    public static YtDlpResolveResult Success(
        string pageUrl,
        string url,
        string? title,
        IReadOnlyList<AudioSidecarSource>? audioSidecars = null,
        IReadOnlyDictionary<string, string>? headers = null) =>
        new(YtDlpResolveStatus.Success, pageUrl, url, title, errorMessage: null, audioSidecars ?? [], headers);

    public TResult Match<TResult>(
        Func<string, string, string?, IReadOnlyList<AudioSidecarSource>, TResult> onSuccess,
        Func<TResult> onNotFound,
        Func<string, TResult> onFailed) =>
        Status switch
        {
            YtDlpResolveStatus.Success => onSuccess(PageUrl, Url!, Title, AudioSidecars),
            YtDlpResolveStatus.NotFound => onNotFound(),
            YtDlpResolveStatus.Failed => onFailed(ErrorMessage!),
            _ => throw new ArgumentOutOfRangeException(nameof(Status), Status, @"Unhandled yt-dlp resolve status.")
        };
}
