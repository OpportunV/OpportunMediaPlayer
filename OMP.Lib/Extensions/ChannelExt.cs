using System.Threading.Channels;

namespace OMP.Lib.Extensions;

internal static class ChannelExt
{
    extension<TWrite>(ChannelWriter<TWrite> channelWriter)
    {
        public bool TryWriteBlocking(TWrite item, CancellationToken token = default)
        {
            try
            {
                channelWriter.WriteAsync(item, token).AsTask().Wait(token);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }

    extension<TRead>(ChannelReader<TRead> channelReader)
    {
        public bool TryReadBlocking(out TRead value, CancellationToken token = default)
        {
            try
            {
                var task = channelReader.ReadAsync(token).AsTask();
                task.Wait(token);
                value = task.Result;
                return true;
            }
            catch (OperationCanceledException)
            {
                value = default!;
                return false;
            }
        }
    }
}
