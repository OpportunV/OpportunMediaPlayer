using OMP.Lib.Session;

namespace OMP.Lib.Tests;

public class EndOfStreamTrackerTests
{
    [Fact]
    public void NewTracker_HasNotReachedEnd()
    {
        var tracker = new EndOfStreamTracker();

        Assert.False(tracker.HasReachedEnd(hasPendingPlayableContent: false));
        Assert.False(tracker.HasReachedEnd(hasPendingPlayableContent: true));
    }

    [Fact]
    public void MarkEndOfStream_WithPendingContent_HasNotReachedEnd()
    {
        var tracker = new EndOfStreamTracker();

        tracker.MarkEndOfStream();

        Assert.False(tracker.HasReachedEnd(hasPendingPlayableContent: true));
    }

    [Fact]
    public void MarkEndOfStream_WithNoPendingContent_HasReachedEnd()
    {
        var tracker = new EndOfStreamTracker();

        tracker.MarkEndOfStream();

        Assert.True(tracker.HasReachedEnd(hasPendingPlayableContent: false));
    }

    [Fact]
    public void MarkStreamReadable_AfterEndOfStream_HasNotReachedEnd()
    {
        var tracker = new EndOfStreamTracker();
        tracker.MarkEndOfStream();

        tracker.MarkStreamReadable();

        Assert.False(tracker.HasReachedEnd(hasPendingPlayableContent: false));
    }

    [Fact]
    public void MarkEndOfStream_AfterResumingWithoutSeeking_HasReachedEndAgain()
    {
        var tracker = new EndOfStreamTracker();
        tracker.MarkEndOfStream();
        Assert.True(tracker.HasReachedEnd(hasPendingPlayableContent: false));

        tracker.MarkEndOfStream();

        Assert.True(tracker.HasReachedEnd(hasPendingPlayableContent: false));
    }
}
