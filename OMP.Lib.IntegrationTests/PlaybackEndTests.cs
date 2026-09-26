using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib.Session;

namespace OMP.Lib.IntegrationTests;

/// <summary>
/// Runs every case against the same file opened both locally and over HTTP, since network
/// sources get their own buffering (deeper lookahead and packet channels, FFmpeg reconnect
/// options) and a user-reported bug - the timeline running past the duration after a web video
/// ended - only ever showed up on network playback.
/// </summary>
public sealed class PlaybackEndTests : IDisposable
{
    private const int SampleIntervalMs = 20;

    private readonly LocalHttpFileServer _server = new(TestFixtures.VideoWithAudioMp4);

    public static TheoryData<bool> Sources => new(false, true);

    [Theory]
    [MemberData(nameof(Sources))]
    public void Play_AdvancesCurrentTime(bool overHttp)
    {
        var registry = CreateRegistry();

        try
        {
            registry.Open(new MediaOpenRequest(SourceFor(overHttp), []));
            var session = registry.Current!;

            Assert.True(session.HasVideo);
            Assert.True(session.Duration > TimeSpan.Zero);

            session.Play();
            Thread.Sleep(500);
            session.Pause();

            Assert.InRange(session.CurrentTime.TotalSeconds, 0.3, 2.0);
        }
        finally
        {
            registry.Close();
        }
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public void PlayingToTheEnd_EndsPlaybackAndNeverReportsAPositionPastTheDuration(bool overHttp)
    {
        var registry = CreateRegistry();

        try
        {
            registry.Open(new MediaOpenRequest(SourceFor(overHttp), []));
            var session = registry.Current!;
            using var ended = new ManualResetEventSlim();
            session.PlaybackEnded += ended.Set;

            session.Seek(session.Duration - TimeSpan.FromSeconds(1.5));
            session.Play();

            var maxSeen = TimeSpan.Zero;
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!ended.IsSet && DateTime.UtcNow < deadline)
            {
                var current = session.CurrentTime;
                if (current > maxSeen)
                {
                    maxSeen = current;
                }

                Thread.Sleep(SampleIntervalMs);
            }

            Assert.True(ended.IsSet, "PlaybackEnded never fired.");
            Assert.True(maxSeen <= session.Duration, $"Position {maxSeen} was reported past the duration {session.Duration}.");
            Assert.Equal(session.Duration, session.CurrentTime);
        }
        finally
        {
            registry.Close();
        }
    }

    public void Dispose() => _server.Dispose();

    private static MediaSessionRegistry CreateRegistry() =>
        new(new PlaybackTuningOptions(), NullLoggerFactory.Instance, NativeLibraryOptionsFactory.Create());

    private string SourceFor(bool overHttp) => overHttp ? _server.Url : TestFixtures.VideoWithAudioMp4;
}
