using System;
using System.Collections.Generic;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;

namespace OMP.Ui.Services;

public sealed record YtDlpResolveResult
{
    public YtDlpResolveStatus Status { get; }

    public string PageUrl { get; }

    public string? Url { get; }

    public string? Title { get; }

    public string? ErrorMessage { get; }

    public IReadOnlyList<AudioSidecarSource> AudioSidecars { get; }

    public IReadOnlyList<SubtitleSidecarSource> SubtitleSidecars { get; }

    public IReadOnlyDictionary<string, string>? Headers { get; }

    private YtDlpResolveResult(
        YtDlpResolveStatus status,
        string pageUrl,
        string? url,
        string? title,
        string? errorMessage,
        IReadOnlyList<AudioSidecarSource> audioSidecars,
        IReadOnlyList<SubtitleSidecarSource> subtitleSidecars,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        Status = status;
        PageUrl = pageUrl;
        Url = url;
        Title = title;
        ErrorMessage = errorMessage;
        AudioSidecars = audioSidecars;
        SubtitleSidecars = subtitleSidecars;
        Headers = headers;
    }

    public static YtDlpResolveResult NotFound(string pageUrl) =>
        new(YtDlpResolveStatus.NotFound, pageUrl, url: null, title: null, errorMessage: null, audioSidecars: [], subtitleSidecars: []);

    public static YtDlpResolveResult Failed(string pageUrl, string message) =>
        new(YtDlpResolveStatus.Failed, pageUrl, url: null, title: null, message, audioSidecars: [], subtitleSidecars: []);

    public static YtDlpResolveResult Success(
        string pageUrl,
        string url,
        string? title,
        IReadOnlyList<AudioSidecarSource>? audioSidecars = null,
        IReadOnlyList<SubtitleSidecarSource>? subtitleSidecars = null,
        IReadOnlyDictionary<string, string>? headers = null) =>
        new(YtDlpResolveStatus.Success, pageUrl, url, title, errorMessage: null, audioSidecars ?? [], subtitleSidecars ?? [], headers);

    public TResult Match<TResult>(
        Func<string, string, string?, IReadOnlyList<AudioSidecarSource>, IReadOnlyList<SubtitleSidecarSource>, TResult> onSuccess,
        Func<TResult> onNotFound,
        Func<string, TResult> onFailed) =>
        Status switch
        {
            YtDlpResolveStatus.Success => onSuccess(PageUrl, Url!, Title, AudioSidecars, SubtitleSidecars),
            YtDlpResolveStatus.NotFound => onNotFound(),
            YtDlpResolveStatus.Failed => onFailed(ErrorMessage!),
            _ => throw new ArgumentOutOfRangeException(nameof(Status), Status, @"Unhandled yt-dlp resolve status.")
        };
}
