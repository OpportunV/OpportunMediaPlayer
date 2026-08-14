using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;

namespace OMP.Lib.Interop;

internal static class FFmpegEnvironment
{
    private static readonly Lock _initSync = new();
    private static bool _initialized;

    public static void EnsureInitialized(ILogger logger)
    {
        lock (_initSync)
        {
            if (_initialized)
            {
                return;
            }

            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_FATAL);
            _initialized = true;

            logger.LogDebug("FFmpeg native log level set to fatal-only.");
        }
    }
}
