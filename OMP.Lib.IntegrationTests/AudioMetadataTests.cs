using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib.Session;

namespace OMP.Lib.IntegrationTests;

public sealed class AudioMetadataTests
{
    [Theory]
    // Per-stream title/language tags.
    [InlineData("video/sample.mkv", "Stereo", "eng")]
    [InlineData("video/sample.webm", "Stereo", "eng")]
    [InlineData("audio/sample.ogg", "Alien Spaceship Atmosphere", "eng")]
    // MP4/MOV track title in udta/name, which FFmpeg doesn't read: next to the default SoundHandler
    // (mp4), and next to a real handler_name that it has to win over (mov, the reporter's case in #16).
    [InlineData("video/sample.mp4", "Stereo", "eng")]
    [InlineData("video/sample.mov", "Stereo", "eng")]
    // handler_name as the only name.
    [InlineData("audio/sample.m4a", "Alien Spaceship Atmosphere", "eng")]
    // Containers that can only carry one of the two.
    [InlineData("video/sample.avi", "Stereo", "Unknown")]
    [InlineData("video/sample.wmv", "Unknown", "eng")]
    // Audio-only files tagged at the container level (ID3, Vorbis comments, INFO chunk).
    [InlineData("audio/sample.mp3", "Alien Spaceship Atmosphere", "eng")]
    [InlineData("audio/sample.flac", "Alien Spaceship Atmosphere", "eng")]
    [InlineData("audio/sample.wav", "Alien Spaceship Atmosphere", "eng")]
    // No per-track metadata at all; the video's container title is the movie's, not the track's.
    [InlineData("video/sample.flv", "Unknown", "Unknown")]
    [InlineData("audio/sample.aac", "Unknown", "Unknown")]
    public void Open_ReadsAudioTrackTitleAndLanguage(string fixture, string expectedTitle, string expectedLanguage)
    {
        IMediaSessionRegistry registry = new MediaSessionRegistry(
            new PlaybackTuningOptions(),
            NullLoggerFactory.Instance,
            NativeLibraryOptionsFactory.Create());

        try
        {
            registry.Open(TestFixtures.Get(fixture));

            var stream = Assert.Single(registry.Current!.AudioStreams);
            Assert.Equal(expectedTitle, stream.Title);
            Assert.Equal(expectedLanguage, stream.Language);
        }
        finally
        {
            registry.Close();
        }
    }
}
