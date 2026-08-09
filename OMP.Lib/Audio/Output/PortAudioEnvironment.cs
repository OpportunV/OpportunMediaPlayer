using Microsoft.Extensions.Logging;
using PortAudioSharp;

namespace OMP.Lib.Audio.Output;

internal static class PortAudioEnvironment
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

            PortAudio.LoadNativeLibrary();
            PortAudio.Initialize();
            _initialized = true;

            logger.LogDebug("PortAudio initialized ({VersionText}).", PortAudio.VersionInfo.versionText);
        }
    }
}
