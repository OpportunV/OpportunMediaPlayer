using OMP.Lib.Audio;
using OMP.Lib.Session;

namespace OMP.Lib.IntegrationTests;

public sealed class MultiSourceSessionTests(MultiSourceSessionFixture fixture) : IClassFixture<MultiSourceSessionFixture>
{
    private const int SettleMs = 50;

    private IMediaSession Session => fixture.Registry.Current!;

    [Fact]
    public void Open_SidecarAudioStreamsAppearWithUniqueIdsAndSuppliedMetadata()
    {
        Assert.True(Session.AudioStreams.Count >= 3, "Expected the primary's own audio stream plus both sidecars.");

        var ids = Session.AudioStreams.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        Assert.Contains(Session.AudioStreams, s => s is { Language: "fr", Title: "French dub" });
        Assert.Contains(Session.AudioStreams, s => s is { Language: "de", Title: "German dub" });
    }

    [SkippableFact]
    public void SidecarStream_RoutesToAnOutput()
    {
        Skip.If(Session.AudioOutputs.Count == 0, "No audio output devices available in this environment.");

        var sidecarStream = Session.AudioStreams.First(s => s.Language == "fr");
        var output = Session.AudioOutputs[0];

        Session.SetAudioRoutes([new AudioRoute(sidecarStream, output)]);
        Thread.Sleep(SettleMs);

        Assert.Single(Session.AudioRoutes);
        Assert.Equal(sidecarStream.Id, Session.AudioRoutes[0].Stream.Id);
    }

    [Fact]
    public void Seek_LandsNearTarget_WithMultipleSourcesOpen()
    {
        var target = TimeSpan.FromSeconds(5);

        Session.Seek(target);
        Thread.Sleep(SettleMs);

        Assert.InRange(Session.CurrentTime.TotalSeconds, target.TotalSeconds - 0.5, target.TotalSeconds + 0.5);
    }
}
