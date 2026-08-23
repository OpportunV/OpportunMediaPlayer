using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib.Session;

namespace OMP.Lib.IntegrationTests;

public sealed class MediaSessionFixture : IDisposable
{
    public IMediaSessionRegistry Registry { get; }

    public MediaSessionFixture()
    {
        Registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(),
            NullLoggerFactory.Instance,
            NativeLibraryOptionsFactory.Create());

        Registry.Open(TestFixtures.VideoWithAudioMp4);
    }

    public void Dispose() => Registry.Close();
}