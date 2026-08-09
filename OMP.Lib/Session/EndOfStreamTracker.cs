namespace OMP.Lib.Session;

internal sealed class EndOfStreamTracker
{
    private bool _demuxAtEndOfStream;

    public void MarkEndOfStream() => _demuxAtEndOfStream = true;

    public void MarkStreamReadable() => _demuxAtEndOfStream = false;

    public bool HasReachedEnd(bool hasPendingPlayableContent) =>
        _demuxAtEndOfStream && !hasPendingPlayableContent;
}
