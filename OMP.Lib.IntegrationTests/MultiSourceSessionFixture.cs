using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib.Session;

namespace OMP.Lib.IntegrationTests;

public sealed class MultiSourceSessionFixture : IDisposable
{
    public IMediaSessionRegistry Registry { get; }

    public MultiSourceSessionFixture()
    {
        Registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(),
            NullLoggerFactory.Instance,
            NativeLibraryOptionsFactory.Create());

        Registry.Open(
            new MediaOpenRequest(
                TestFixtures.VideoWithAudioMp4,
                [
                    new AudioSidecarSource(TestFixtures.AudioSidecarMp3, Language: "fr", Title: "French dub"),
                    new AudioSidecarSource(TestFixtures.AudioSidecarFlac, Language: "de", Title: "German dub")
                ]));
    }

    public void Dispose() => Registry.Close();
}
