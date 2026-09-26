using Microsoft.Extensions.Logging.Abstractions;
using OMP.Lib.Subtitle;
using OMP.Ui.Services;
using OMP.Ui.Tests.TestDoubles;

namespace OMP.Ui.Tests.Services;

public class SubtitleRouteApplierTests
{
    private static readonly SubtitleRoute _english = new(new SubtitleStream(1, "Unknown", "English", "en", IsTextBased: true), "bottom");
    private static readonly SubtitleRoute _russian = new(new SubtitleStream(2, "Unknown", "Russian", "ru", IsTextBased: true), "top");

    [Fact]
    public async Task EverythingApplies_NoRetry()
    {
        var h = new Harness();

        var applied = await h.Applier.ApplyAsync(h.Session, [_english]);

        Assert.Equal([_english], applied);
        Assert.Single(h.Session.AppliedSubtitleRoutes);
        Assert.Empty(h.Delays);
    }

    [Fact]
    public async Task ARouteFailsThenSucceeds_RetriesWithBackoffUntilItApplies()
    {
        var h = new Harness();
        var attempts = 0;
        h.Session.SubtitleRouteOutcome = requested => ++attempts < 3 ? requested.Where(r => r != _russian).ToList() : requested;
        var retryingCallbacks = 0;

        var applied = await h.Applier.ApplyAsync(h.Session, [_english, _russian], () => retryingCallbacks++);

        Assert.Equal([_english, _russian], applied);
        Assert.Equal(3, h.Session.AppliedSubtitleRoutes.Count);
        Assert.Equal(SubtitleRouteApplier.RetryDelays.Take(2), h.Delays);
        Assert.Equal(2, retryingCallbacks);
    }

    [Fact]
    public async Task ARouteNeverApplies_GivesUpAfterTheLastRetryAndReturnsWhatDidApply()
    {
        var h = new Harness();
        h.Session.SubtitleRouteOutcome = requested => requested.Where(r => r != _russian).ToList();

        var applied = await h.Applier.ApplyAsync(h.Session, [_english, _russian]);

        Assert.Equal([_english], applied);
        Assert.Equal(SubtitleRouteApplier.RetryDelays.Count + 1, h.Session.AppliedSubtitleRoutes.Count);
        Assert.Equal(SubtitleRouteApplier.RetryDelays, h.Delays);
    }

    [Fact]
    public async Task ANewerApply_SupersedesAPendingRetry()
    {
        var h = new Harness();
        var release = new TaskCompletionSource();
        h.DelayOverride = _ => release.Task;
        h.Session.SubtitleRouteOutcome = requested => requested.Where(r => r != _russian).ToList();

        var first = h.Applier.ApplyAsync(h.Session, [_russian]);
        await WaitUntil(() => h.Delays.Count == 1);

        h.Session.SubtitleRouteOutcome = requested => requested;
        var second = await h.Applier.ApplyAsync(h.Session, [_english]);
        release.SetResult();

        Assert.Null(await first);
        Assert.Equal([_english], second);
        Assert.Equal([_english], h.Session.AppliedSubtitleRoutes.Last());
    }

    [Fact]
    public async Task TheSessionChanged_StopsRetryingAgainstTheOldOne()
    {
        var h = new Harness();
        var release = new TaskCompletionSource();
        h.DelayOverride = _ => release.Task;
        h.Session.SubtitleRouteOutcome = _ => [];

        var pending = h.Applier.ApplyAsync(h.Session, [_russian]);
        await WaitUntil(() => h.Delays.Count == 1);

        h.Registry.Current = new FakeMediaSession();
        release.SetResult();

        Assert.Null(await pending);
        Assert.Single(h.Session.AppliedSubtitleRoutes);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException();
            }

            await Task.Delay(5);
        }
    }

    private sealed class Harness
    {
        public FakeMediaSession Session { get; } = new();

        public FakeMediaSessionRegistry Registry { get; } = new();

        public List<TimeSpan> Delays { get; } = [];

        public Func<TimeSpan, Task>? DelayOverride { get; set; }

        public SubtitleRouteApplier Applier { get; }

        public Harness()
        {
            Registry.Current = Session;
            Applier = new SubtitleRouteApplier(
                Registry,
                NullLoggerFactory.Instance,
                delay =>
                {
                    lock (Delays)
                    {
                        Delays.Add(delay);
                    }

                    return DelayOverride?.Invoke(delay) ?? Task.CompletedTask;
                });
        }
    }
}
