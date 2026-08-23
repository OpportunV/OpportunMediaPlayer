using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib;
using OMP.Lib.Session;
using OMP.Ui.Services;
using OMP.Ui.Tests.TestDoubles;

namespace OMP.Ui.Tests.Services;

public class MainWindowCommandsTests
{
    [Fact]
    public async Task TogglePlayPause_WhenPaused_PlaysAndUpdatesContext()
    {
        var (commands, session, context) = CreateAttached();

        await commands.TogglePlayPauseAsync();

        Assert.Equal(1, session.PlayCallCount);
        Assert.True(context.IsPlaying);
    }

    [Fact]
    public async Task TogglePlayPause_WhenPlaying_PausesAndUpdatesContext()
    {
        var (commands, session, context) = CreateAttached();
        context.IsPlaying = true;

        await commands.TogglePlayPauseAsync();

        Assert.Equal(1, session.PauseCallCount);
        Assert.False(context.IsPlaying);
    }

    [Fact]
    public async Task TogglePlayPause_AtEndOfDuration_SeeksToZeroBeforePlaying()
    {
        var (commands, session, _) = CreateAttached();
        session.Duration = TimeSpan.FromSeconds(60);
        session.CurrentTime = TimeSpan.FromSeconds(60);

        await commands.TogglePlayPauseAsync();

        Assert.Equal(TimeSpan.Zero, session.LastSeekTarget);
        Assert.Equal(1, session.PlayCallCount);
    }

    [Fact]
    public async Task TogglePlayPause_NoCurrentSession_DoesNothing()
    {
        IMediaSessionRegistry registry = new FakeMediaSessionRegistry();
        var commands = new MainWindowCommands(registry, NullLogger<MainWindowCommands>.Instance);
        commands.Attach(new RecordingCommandContext().ToContext());

        await commands.TogglePlayPauseAsync();
    }

    [Fact]
    public async Task StepBack_CallsSessionStepWithNegativeFiveSeconds()
    {
        var (commands, session, _) = CreateAttached();

        await commands.StepBackAsync();

        Assert.Equal(TimeSpan.FromSeconds(-5), session.LastStepOffset);
    }

    [Fact]
    public async Task StepForward_CallsSessionStepWithPositiveFiveSeconds()
    {
        var (commands, session, _) = CreateAttached();

        await commands.StepForwardAsync();

        Assert.Equal(TimeSpan.FromSeconds(5), session.LastStepOffset);
    }

    [Fact]
    public async Task IncreaseSpeed_AppliesNextPresetAboveCurrent()
    {
        var (commands, session, context) = CreateAttached();
        session.SetSpeed(1.0);

        await commands.IncreaseSpeedAsync();

        Assert.Equal(PlaybackSpeedPresets.Next(1.0), session.Speed);
        Assert.Equal(session.Speed, context.LastSpeedDisplay);
    }

    [Fact]
    public async Task DecreaseSpeed_AppliesPreviousPresetBelowCurrent()
    {
        var (commands, session, context) = CreateAttached();
        session.SetSpeed(1.0);

        await commands.DecreaseSpeedAsync();

        Assert.Equal(PlaybackSpeedPresets.Previous(1.0), session.Speed);
        Assert.Equal(session.Speed, context.LastSpeedDisplay);
    }

    [Fact]
    public async Task SetSpeed_AppliesGivenSpeedAndUpdatesDisplay()
    {
        var (commands, session, context) = CreateAttached();

        await commands.ApplySpeedAsync(1.75);

        Assert.Equal(1.75, session.Speed);
        Assert.Equal(1.75, context.LastSpeedDisplay);
    }

    [Fact]
    public async Task ResetSpeed_SetsSpeedToOne()
    {
        var (commands, session, _) = CreateAttached();
        session.SetSpeed(2.0);

        await commands.ApplySpeedAsync(1.0);

        Assert.Equal(1.0, session.Speed);
    }

    [Fact]
    public void SetMasterVolume_SetsSessionVolume()
    {
        var (commands, session, _) = CreateAttached();

        commands.SetMasterVolume(0.4);

        Assert.Equal(0.4, session.MasterVolume);
    }

    [Fact]
    public void IncreaseMasterVolume_AddsStepAndUpdatesDisplay()
    {
        var (commands, session, context) = CreateAttached();
        session.SetMasterVolume(0.5);

        commands.IncreaseMasterVolume();

        Assert.Equal(0.55, session.MasterVolume, 3);
        Assert.Equal(session.MasterVolume, context.LastVolumeDisplay);
    }

    [Fact]
    public void DecreaseMasterVolume_SubtractsStepAndUpdatesDisplay()
    {
        var (commands, session, context) = CreateAttached();
        session.SetMasterVolume(0.5);

        commands.DecreaseMasterVolume();

        Assert.Equal(0.45, session.MasterVolume, 3);
        Assert.Equal(session.MasterVolume, context.LastVolumeDisplay);
    }

    [Fact]
    public void ToggleMute_TogglesSessionMutedAndUpdatesContext()
    {
        var (commands, session, context) = CreateAttached();

        commands.ToggleMute();

        Assert.True(session.IsMuted);
        Assert.True(context.LastSetIsMuted);

        commands.ToggleMute();

        Assert.False(session.IsMuted);
        Assert.False(context.LastSetIsMuted);
    }

    [Fact]
    public void ToggleSubtitles_InvokesContextToggleSubtitles()
    {
        var (commands, _, context) = CreateAttached();

        commands.ToggleSubtitles();

        Assert.Equal(1, context.ToggleSubtitlesCallCount);
    }

    [Fact]
    public void ToggleFullscreen_InvokesContextToggleFullscreen()
    {
        var (commands, _, context) = CreateAttached();

        commands.ToggleFullscreen();

        Assert.Equal(1, context.ToggleFullscreenCallCount);
        Assert.True(context.IsFullscreen);
    }

    [Fact]
    public void ExitFullscreen_WhenFullscreen_TogglesOff()
    {
        var (commands, _, context) = CreateAttached();
        context.IsFullscreen = true;

        commands.ExitFullscreen();

        Assert.False(context.IsFullscreen);
        Assert.Equal(1, context.ToggleFullscreenCallCount);
    }

    [Fact]
    public void ExitFullscreen_WhenNotFullscreen_DoesNotToggle()
    {
        var (commands, _, context) = CreateAttached();

        commands.ExitFullscreen();

        Assert.Equal(0, context.ToggleFullscreenCallCount);
    }

    private static (MainWindowCommands Commands, FakeMediaSession Session, RecordingCommandContext Context) CreateAttached()
    {
        var session = new FakeMediaSession();
        var registry = new FakeMediaSessionRegistry { Current = session };
        var commands = new MainWindowCommands(registry, NullLogger<MainWindowCommands>.Instance);
        var context = new RecordingCommandContext();
        commands.Attach(context.ToContext());
        return (commands, session, context);
    }
}
