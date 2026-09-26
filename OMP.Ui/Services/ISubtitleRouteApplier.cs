using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;

namespace OMP.Ui.Services;

public interface ISubtitleRouteApplier
{
    /// <summary>
    /// Applies <paramref name="routes"/> off the UI thread, retrying with backoff while some of
    /// them fail to open (a web caption request answered with HTTP 429 usually succeeds a few
    /// seconds later). <paramref name="onRetrying"/> runs, off the UI thread, before each wait.
    /// Returns the routes that finally applied, or <see langword="null"/> when a newer call or a
    /// different session superseded this one - its outcome no longer matters to anyone.
    /// </summary>
    public Task<IReadOnlyList<SubtitleRoute>?> ApplyAsync(
        IMediaSession session, IReadOnlyList<SubtitleRoute> routes, Action? onRetrying = null);
}
