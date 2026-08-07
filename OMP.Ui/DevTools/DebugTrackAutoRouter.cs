using System;
using System.Linq;
using Microsoft.Extensions.Options;
using OMP.Lib.Session;

namespace OMP.Ui.DevTools;

// Dev convenience: when enabled via config, auto-routes every audio stream on open instead of
// requiring routes to be added by hand each debug run.
internal sealed class DebugTrackAutoRouter
{
    public DebugTrackAutoRouter(IMediaSessionRegistry registry, IOptions<DebugOptions> options)
    {
        if (!options.Value.AutoRouteAllTracks)
        {
            return;
        }

        registry.SessionChanged += OnSessionChanged;
    }

    private static void OnSessionChanged(IMediaSessionRegistry registry)
    {
        var session = registry.Current;
        if (session is null)
        {
            return;
        }

        var routes = session.AudioStreams
            .Zip(session.AudioOutputs)
            .ToList();

        Console.WriteLine($"Audio streams count: {routes.Count}");
        Console.WriteLine(
            string.Join(
                Environment.NewLine,
                session.AudioStreams.Select(stream => $"{stream.Title}, {stream.Language}")));
        Console.WriteLine();
        Console.WriteLine($"Audio outputs count: {routes.Count}");
        Console.WriteLine(
            string.Join(Environment.NewLine, session.AudioOutputs.Select(output => output.FriendlyName)));

        Console.WriteLine("Resulting routes:");
        Console.WriteLine(
            string.Join(
                Environment.NewLine,
                routes.Select(route => $"{route.First.Title} -> {route.Second.FriendlyName}")));

        session.SetAudioRoutes(routes);
    }
}
