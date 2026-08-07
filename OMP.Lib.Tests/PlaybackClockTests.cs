using OMP.Lib.Session;

namespace OMP.Lib.Tests;

public class PlaybackClockTests
{
    [Fact]
    public void NewClock_IsStoppedAtZero()
    {
        var clock = new PlaybackClock();

        Assert.False(clock.IsRunning);
        Assert.Equal(0, clock.CurrentSeconds);
        Assert.Equal(1.0, clock.Speed);
    }

    [Fact]
    public void Rebase_SetsExactBaseAndStopsClock()
    {
        var clock = new PlaybackClock();
        clock.Start();

        clock.Rebase(42.5);

        Assert.False(clock.IsRunning);
        Assert.Equal(42.5, clock.CurrentSeconds);
    }

    [Fact]
    public void Stop_CapturesCurrentTimeAsNewBase()
    {
        var clock = new PlaybackClock();
        clock.Rebase(10);
        clock.Start();

        Thread.Sleep(30);
        clock.Stop();
        var stoppedAt = clock.CurrentSeconds;

        Assert.False(clock.IsRunning);
        Assert.True(stoppedAt >= 10);

        Thread.Sleep(30);
        Assert.Equal(stoppedAt, clock.CurrentSeconds);
    }

    [Fact]
    public void Start_AdvancesCurrentSecondsOverRealTime()
    {
        var clock = new PlaybackClock();
        clock.Rebase(5);
        clock.Start();

        Thread.Sleep(100);

        Assert.InRange(clock.CurrentSeconds, 5.05, 5.5);
    }

    [Fact]
    public void SetSpeed_ChangesSpeedImmediately()
    {
        var clock = new PlaybackClock();

        clock.SetSpeed(1.5);

        Assert.Equal(1.5, clock.Speed);
    }

    [Fact]
    public void SetSpeed_WhileStopped_DoesNotStartClock()
    {
        var clock = new PlaybackClock();
        clock.Rebase(7);

        clock.SetSpeed(2.0);

        Assert.False(clock.IsRunning);
        Assert.Equal(7, clock.CurrentSeconds);
    }

    [Fact]
    public void SetSpeed_WhileRunning_PreservesElapsedBaseAndKeepsRunning()
    {
        var clock = new PlaybackClock();
        clock.Rebase(0);
        clock.Start();

        Thread.Sleep(50);
        clock.SetSpeed(2.0);
        var justAfterSpeedChange = clock.CurrentSeconds;

        Assert.True(clock.IsRunning);
        Assert.True(justAfterSpeedChange >= 0.05);

        Thread.Sleep(50);
        Assert.True(clock.CurrentSeconds > justAfterSpeedChange);
    }
}
