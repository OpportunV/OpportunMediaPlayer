namespace OMP.Lib.Threading;

internal sealed class PipelineWorker(PipelineWorkerRole role, CancellationToken cancellationToken) : IDisposable
{
    public PipelineWorkerRole Role => role;

    private readonly ManualResetEventSlim _resumeGate = new(true);
    private Thread? _thread;

    public void Start(Action<PipelineWorker> loopBody, string? threadName = null)
    {
        _thread = new Thread(() => loopBody(this)) { IsBackground = true, Name = threadName ?? role.ToString() };
        _thread.Start();
    }

    public bool TryWaitIfPaused()
    {
        try
        {
            _resumeGate.Wait(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public void Pause() => _resumeGate.Reset();

    public void Resume() => _resumeGate.Set();

    public void Join() => _thread?.Join();

    public void Dispose() => _resumeGate.Dispose();
}
