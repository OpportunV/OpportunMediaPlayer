using System.Threading.Channels;
using OMP.Lib.Extensions;

namespace OMP.Lib.Tests;

public class ChannelExtTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(2);

    [Fact]
    public void TryWriteBlocking_ThenTryReadBlocking_RoundTripsTheItem()
    {
        var channel = Channel.CreateUnbounded<int>();

        var wrote = channel.Writer.TryWriteBlocking(42);
        var read = channel.Reader.TryReadBlocking(out var value);

        Assert.True(wrote);
        Assert.True(read);
        Assert.Equal(42, value);
    }

    [Fact]
    public void TryReadBlocking_ReturnsFalse_WhenCancelledBeforeAnyItemArrives()
    {
        var channel = Channel.CreateUnbounded<int>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var read = channel.Reader.TryReadBlocking(out var value, cts.Token);

        Assert.False(read);
        Assert.Equal(0, value);
    }

    [Fact]
    public async Task TryReadBlocking_ReturnsFalse_WhenCancelledWhileBlockedWaitingForAnItem()
    {
        var channel = Channel.CreateUnbounded<int>();
        using var cts = new CancellationTokenSource();

        var readTask = Task.Run(() => channel.Reader.TryReadBlocking(out _, cts.Token));

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var read = await readTask.WaitAsync(_timeout);
        Assert.False(read);
    }

    [Fact]
    public async Task TryWriteBlocking_ReturnsFalse_WhenCancelledWhileBlockedOnAFullChannel()
    {
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        using var cts = new CancellationTokenSource();

        Assert.True(channel.Writer.TryWriteBlocking(1));

        var writeTask = Task.Run(() => channel.Writer.TryWriteBlocking(2, cts.Token));

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var wrote = await writeTask.WaitAsync(_timeout);
        Assert.False(wrote);
    }
}
