using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OMP.Lib.Session;

namespace OMP.Ui.DevTools;

// Dev convenience: when enabled via config, auto-routes every audio stream on open instead of
// requiring routes to be added by hand each debug run.
internal sealed class DebugTrackAutoRouter
{
    private readonly ILogger<DebugTrackAutoRouter> _logger;

    public DebugTrackAutoRouter(
        IMediaSessionRegistry registry,
        IOptions<DebugOptions> options,
        ILogger<DebugTrackAutoRouter> logger)
    {
        _logger = logger;

        if (!options.Value.AutoRouteAllTracks)
        {
            return;
        }

        registry.SessionChanged += OnSessionChanged;
    }

    private void OnSessionChanged(IMediaSessionRegistry registry)
    {
        var session = registry.Current;
        if (session is null)
        {
            return;
        }

        var routes = session.AudioStreams
            .Zip(session.AudioOutputs)
            .ToList();

        if (routes.Count == 0)
        {
            _logger.LogWarning(
                "Auto-routing produced no routes: {StreamCount} audio stream(s), {OutputCount} output(s).",
                session.AudioStreams.Count,
                session.AudioOutputs.Count);
            return;
        }

        _logger.LogInformation(
            "Auto-routing enabled: {StreamCount} audio stream(s), {OutputCount} output(s), {RouteCount} route(s).",
            session.AudioStreams.Count,
            session.AudioOutputs.Count,
            routes.Count);

        foreach (var (stream, output) in routes)
        {
            _logger.LogDebug(
                "Auto-route: '{Title}' [{Language}] -> '{FriendlyName}'.",
                stream.Title,
                stream.Language,
                output.FriendlyName);
        }

        session.SetAudioRoutes(routes);
    }
}
