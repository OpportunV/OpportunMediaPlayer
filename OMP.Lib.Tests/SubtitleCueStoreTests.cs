using OMP.Lib.Subtitle;

namespace OMP.Lib.Tests;

public class SubtitleCueStoreTests
{
    [Fact]
    public void GetActive_TimeWithinCueInterval_ReturnsCue()
    {
        var store = new SubtitleCueStore();
        var cue = MakeCue(start: 1, end: 3);
        store.Add(cue);

        Assert.Equal([cue], store.GetActive(2));
    }

    [Fact]
    public void GetActive_TimeBeforeStart_DoesNotReturnCue()
    {
        var store = new SubtitleCueStore();
        store.Add(MakeCue(start: 1, end: 3));

        Assert.Empty(store.GetActive(0.5));
    }

    [Fact]
    public void GetActive_TimeAtOrAfterEnd_DoesNotReturnCue()
    {
        var store = new SubtitleCueStore();
        store.Add(MakeCue(start: 1, end: 3));

        Assert.Empty(store.GetActive(3));
    }

    [Fact]
    public void GetActive_OverlappingCues_ReturnsBoth()
    {
        var store = new SubtitleCueStore();
        var shortCue = MakeCue(start: 1, end: 2);
        var longCue = MakeCue(start: 0.5, end: 4);
        store.Add(shortCue);
        store.Add(longCue);

        var active = store.GetActive(1.5);

        Assert.Equal(2, active.Count);
        Assert.Contains(shortCue, active);
        Assert.Contains(longCue, active);
    }

    [Fact]
    public void Add_DuplicateCueAfterReDecode_IsNotDuplicated()
    {
        var store = new SubtitleCueStore();
        store.Add(MakeCue(start: 1, end: 3));
        store.Add(MakeCue(start: 1, end: 3));

        Assert.Single(store.GetActive(2));
    }

    [Fact]
    public void Add_OutOfStartOrder_StillQueriesCorrectly()
    {
        var store = new SubtitleCueStore();
        var later = MakeCue(start: 5, end: 6);
        var earlier = MakeCue(start: 1, end: 2);
        store.Add(later);
        store.Add(earlier);

        Assert.Equal([earlier], store.GetActive(1.5));
        Assert.Equal([later], store.GetActive(5.5));
    }

    [Fact]
    public void Clear_RemovesAllCues()
    {
        var store = new SubtitleCueStore();
        store.Add(MakeCue(start: 1, end: 3));

        store.Clear();

        Assert.Empty(store.GetActive(2));
    }

    private static SubtitleCue MakeCue(double start, double end)
    {
        return new SubtitleCue("zone", [new SubtitleLine([new SubtitleRun("text", false, false)])], start, end);
    }
}
