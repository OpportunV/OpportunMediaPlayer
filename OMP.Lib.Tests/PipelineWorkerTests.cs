using OMP.Lib.Threading;

namespace OMP.Lib.Tests;

public class PipelineWorkerTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

    [Fact]
    public void Worker_RunsImmediately_WhenNotPaused()
    {
        using var cts = new CancellationTokenSource();
        using var worker = new PipelineWorker(PipelineWorkerRole.Demux, cts.Token);
        var counter = 0;

        worker.Start(w => CountingLoop(w, cts.Token, ref counter));

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counter) > 5, _timeout));

        cts.Cancel();
        worker.Join();
    }

    [Fact]
    public void Pause_HaltsProgress()
    {
        using var cts = new CancellationTokenSource();
        using var worker = new PipelineWorker(PipelineWorkerRole.Demux, cts.Token);
        var counter = 0;

        worker.Start(w => CountingLoop(w, cts.Token, ref counter));

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counter) > 0, _timeout));

        worker.Pause();
        Thread.Sleep(20);
        var countAfterPause = Volatile.Read(ref counter);
        Thread.Sleep(100);

        Assert.Equal(countAfterPause, Volatile.Read(ref counter));

        cts.Cancel();
        worker.Join();
    }

    [Fact]
    public void Resume_UnblocksPausedWorker()
    {
        using var cts = new CancellationTokenSource();
        using var worker = new PipelineWorker(PipelineWorkerRole.Demux, cts.Token);
        worker.Pause();
        var counter = 0;

        worker.Start(w => CountingLoop(w, cts.Token, ref counter));

        Thread.Sleep(50);
        Assert.Equal(0, Volatile.Read(ref counter));

        worker.Resume();

        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref counter) > 0, _timeout));

        cts.Cancel();
        worker.Join();
    }

    [Fact]
    public void Cancel_TerminatesThreadPromptly_EvenWhilePaused()
    {
        using var cts = new CancellationTokenSource();
        using var worker = new PipelineWorker(PipelineWorkerRole.Demux, cts.Token);
        worker.Pause();
        var counter = 0;

        worker.Start(w => CountingLoop(w, cts.Token, ref counter));

        var joined = new ManualResetEventSlim(false);
        var joinThread = new Thread(() =>
        {
            worker.Join();
            joined.Set();
        });
        joinThread.Start();

        cts.Cancel();

        Assert.True(joined.Wait(_timeout));
        joinThread.Join();
    }

    private static void CountingLoop(PipelineWorker worker, CancellationToken token, ref int counter)
    {
        while (!token.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
            }

            Interlocked.Increment(ref counter);
            Thread.Sleep(1);
        }
    }
}
