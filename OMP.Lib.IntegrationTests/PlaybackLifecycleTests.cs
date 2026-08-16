using OMP.Lib.Audio;
using OMP.Lib.Session;
using OMP.Lib.Video;

namespace OMP.Lib.IntegrationTests;

public sealed class PlaybackLifecycleTests(MediaSessionFixture fixture) : IClassFixture<MediaSessionFixture>
{
    private const int SettleMs = 50;

    private IMediaSession Session => fixture.Registry.Current!;

    [Fact]
    public void Open_PopulatesBasicMetadata()
    {
        Assert.True(Session.Duration > TimeSpan.Zero);
        Assert.True(Session.HasVideo);
        Assert.NotEmpty(Session.AudioStreams);
    }

    [Fact]
    public void Play_AdvancesCurrentTime()
    {
        Session.Seek(TimeSpan.Zero);
        Thread.Sleep(SettleMs);

        Session.Play();
        Thread.Sleep(500);
        Session.Pause();
        Thread.Sleep(SettleMs);

        Assert.InRange(Session.CurrentTime.TotalSeconds, 0.3, 2.0);
    }

    [Fact]
    public void Seek_LandsNearTarget()
    {
        var target = TimeSpan.FromSeconds(5);

        Session.Seek(target);
        Thread.Sleep(SettleMs);

        Assert.InRange(Session.CurrentTime.TotalSeconds, target.TotalSeconds - 0.5, target.TotalSeconds + 0.5);
    }

    [Fact]
    public void SetSpeed_ClampsToConfiguredLimits()
    {
        Session.SetSpeed(PlaybackSpeedLimits.Max + 10);
        Thread.Sleep(SettleMs);
        Assert.Equal(PlaybackSpeedLimits.Max, Session.Speed);

        Session.SetSpeed(PlaybackSpeedLimits.Min - 10);
        Thread.Sleep(SettleMs);
        Assert.Equal(PlaybackSpeedLimits.Min, Session.Speed);

        Session.SetSpeed(1);
        Thread.Sleep(SettleMs);
        Assert.Equal(1, Session.Speed);
    }

    [Fact]
    public void SetSpeed_AdvancesCurrentTimeFaster()
    {
        Session.Seek(TimeSpan.Zero);
        Thread.Sleep(SettleMs);

        Session.SetSpeed(2);
        Thread.Sleep(SettleMs);

        Session.Play();
        Thread.Sleep(500);
        Session.Pause();
        Thread.Sleep(SettleMs);

        Session.SetSpeed(1);
        Thread.Sleep(SettleMs);

        Assert.True(Session.CurrentTime.TotalSeconds > 0.6);
    }

    [SkippableFact]
    public void Volume_AppliesWhenOutputAvailable()
    {
        Skip.If(Session.AudioOutputs.Count == 0, "No audio output devices available in this environment.");

        var outputId = Session.AudioOutputs[0].Id;

        Session.SetOutputVolume(outputId, 0.5);
        Session.SetMasterVolume(0.8);

        Assert.Equal(0.5, Session.OutputVolumes[outputId].Volume);
        Assert.Equal(0.8, Session.MasterVolume);

        Session.SetMasterVolume(1.0);
    }

    [SkippableFact]
    public void MultiOutputRouting_RoutesSameStreamToTwoOutputsSimultaneously()
    {
        Skip.If(Session.AudioOutputs.Count < 2, "Fewer than 2 audio output devices available in this environment.");

        var stream = Session.AudioStreams[0];
        var firstOutput = Session.AudioOutputs[0];
        var secondOutput = Session.AudioOutputs[1];

        Session.SetAudioRoutes(
        [
            new AudioRoute(stream, firstOutput),
            new AudioRoute(stream, secondOutput)
        ]);
        Thread.Sleep(SettleMs);

        Assert.Equal(2, Session.AudioRoutes.Count);
        Assert.Contains(Session.AudioRoutes, r => r.Output.Id == firstOutput.Id);
        Assert.Contains(Session.AudioRoutes, r => r.Output.Id == secondOutput.Id);

        Session.SetOutputVolume(firstOutput.Id, 0.3);
        Session.SetOutputVolume(secondOutput.Id, 1.0);

        Assert.Equal(0.3, Session.OutputVolumes[firstOutput.Id].Volume);
        Assert.Equal(1.0, Session.OutputVolumes[secondOutput.Id].Volume);
    }

    [Fact]
    public void VideoFrames_StayWithinToleranceOfClock()
    {
        Session.Seek(TimeSpan.Zero);
        Thread.Sleep(SettleMs);

        var frameCount = 0;
        var maxDriftSeconds = 0.0;

        void OnFrame(VideoFrame frame)
        {
            frameCount++;
            var drift = Math.Abs(frame.TimeSeconds - Session.CurrentTime.TotalSeconds);
            maxDriftSeconds = Math.Max(maxDriftSeconds, drift);
        }

        Session.VideoFrameReady += OnFrame;
        try
        {
            Session.Play();
            Thread.Sleep(800);
            Session.Pause();
        }
        finally
        {
            Session.VideoFrameReady -= OnFrame;
        }

        Assert.True(frameCount > 0, "Expected at least one video frame to be rendered.");
        Assert.True(maxDriftSeconds < 0.5, $"Video frame drift too high: {maxDriftSeconds}s");
    }
}
