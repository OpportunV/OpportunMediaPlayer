using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib.Audio;
using OMP.Lib.Session;

namespace OMP.Lib.IntegrationTests;

/// <summary>
/// Regression coverage for a crash where a lazily-opened sidecar's <c>SourceId</c> (reserved
/// sequentially when the session opens) no longer matches its position in the internal source
/// list once sidecars can be opened out of that reserved order - e.g. routing the second sidecar
/// without ever routing the first. Uses its own (non-shared) session per test so routing order
/// is fully controlled, rather than the shared <see cref="MultiSourceSessionFixture"/>.
/// </summary>
public sealed class LazySidecarOrderingTests
{
    private const int SettleMs = 50;

    [SkippableFact]
    public void Seek_AfterRoutingOnlyTheSecondReservedSidecar_DoesNotCrash()
    {
        var registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(), NullLoggerFactory.Instance, NativeLibraryOptionsFactory.Create());

        try
        {
            registry.Open(
                new MediaOpenRequest(
                    TestFixtures.VideoWithAudioMp4,
                    [
                        new AudioSidecarSource(TestFixtures.AudioSidecarMp3, Language: "fr", Title: "French dub"),
                        new AudioSidecarSource(TestFixtures.AudioSidecarFlac, Language: "de", Title: "German dub")
                    ]));

            var session = registry.Current!;
            Skip.If(session.AudioOutputs.Count == 0, "No audio output devices available in this environment.");

            var germanStream = session.AudioStreams.First(s => s.Language == "de");
            session.SetAudioRoutes([new AudioRoute(germanStream, session.AudioOutputs[0])]);
            Thread.Sleep(SettleMs);

            var target = TimeSpan.FromSeconds(3);
            session.Seek(target);
            Thread.Sleep(SettleMs);

            Assert.InRange(session.CurrentTime.TotalSeconds, target.TotalSeconds - 0.5, target.TotalSeconds + 0.5);
        }
        finally
        {
            registry.Close();
        }
    }
}
