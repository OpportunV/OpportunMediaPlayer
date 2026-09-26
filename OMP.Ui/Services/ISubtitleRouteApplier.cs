using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;

namespace OMP.Ui.Services;

public interface ISubtitleRouteApplier
{
    public Task<IReadOnlyList<SubtitleRoute>?> ApplyAsync(
        IMediaSession session, IReadOnlyList<SubtitleRoute> routes, Action? onRetrying = null);
}
