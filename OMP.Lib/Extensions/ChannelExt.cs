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
                channelWriter.WriteAsync(item, token).AsTask().GetAwaiter().GetResult();
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
                value = channelReader.ReadAsync(token).AsTask().GetAwaiter().GetResult();
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
