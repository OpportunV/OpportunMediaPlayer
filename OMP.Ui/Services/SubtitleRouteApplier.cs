using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;

namespace OMP.Ui.Services;

internal sealed class SubtitleRouteApplier(
    IMediaSessionRegistry mediaSessionRegistry,
    ILoggerFactory loggerFactory,
    Func<TimeSpan, Task>? delay = null) : ISubtitleRouteApplier
{
    public static IReadOnlyList<TimeSpan> RetryDelays { get; } =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8)];

    private readonly ILogger _logger = loggerFactory.CreateLogger<SubtitleRouteApplier>();
    private readonly Func<TimeSpan, Task> _delay = delay ?? (d => Task.Delay(d));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _version;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SubtitleRoute>?> ApplyAsync(
        IMediaSession session, IReadOnlyList<SubtitleRoute> routes, Action? onRetrying = null)
    {
        var version = Interlocked.Increment(ref _version);

        for (var attempt = 0; ; attempt++)
        {
            IReadOnlyList<SubtitleRoute> applied;

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsSuperseded(session, version))
                {
                    return null;
                }

                applied = await Task.Run(() => session.SetSubtitleRoutes(routes)).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }

            var missing = routes.Count(route => !applied.Any(a => a.Stream.Id == route.Stream.Id && a.ZoneId == route.ZoneId));
            if (missing == 0 || attempt >= RetryDelays.Count)
            {
                return applied;
            }

            _logger.LogInformation(
                "{MissingCount} subtitle route(s) failed to apply; retrying in {Delay} (attempt {Attempt} of {MaxAttempts}).",
                missing,
                RetryDelays[attempt],
                attempt + 2,
                RetryDelays.Count + 1);

            onRetrying?.Invoke();
            await _delay(RetryDelays[attempt]).ConfigureAwait(false);
        }
    }

    private bool IsSuperseded(IMediaSession session, int version) =>
        version != Volatile.Read(ref _version) || !ReferenceEquals(mediaSessionRegistry.Current, session);
}
