using System.Threading.Channels;

namespace OMP.Lib.Extensions;

public static class ChannelExt
{
    extension<TWrite>(ChannelWriter<TWrite> channelWriter)
    {
        public void Write(TWrite item, CancellationToken token = default)
        {
            try
            {
                channelWriter.WriteAsync(item, token).AsTask().Wait(token);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    extension<TRead>(ChannelReader<TRead> channelReader)
    {
        public TRead Read(CancellationToken token = default)
        {
            try
            {
                var task = channelReader.ReadAsync(token).AsTask();
                task.Wait(token);
                return task.Result;
            }
            catch (OperationCanceledException)
            {
                return default!;
            }
        }
    }
}