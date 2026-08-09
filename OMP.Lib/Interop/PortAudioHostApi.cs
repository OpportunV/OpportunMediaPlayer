using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace OMP.Lib.Interop;

internal static class PortAudioHostApi
{
    private const int WasapiTypeId = 13;
    private const int TypeOffset = 4;

    public static int? TryGetWasapiHostApiIndex(ILogger logger)
    {
        if (!NativeLibrary.TryLoad("portaudio", out var handle))
        {
            logger.LogWarning("Could not load the portaudio native library to resolve the WASAPI host API.");
            return null;
        }

        var getCount = Marshal.GetDelegateForFunctionPointer<GetHostApiCountDelegate>(
            NativeLibrary.GetExport(handle, "Pa_GetHostApiCount"));
        var getInfo = Marshal.GetDelegateForFunctionPointer<GetHostApiInfoDelegate>(
            NativeLibrary.GetExport(handle, "Pa_GetHostApiInfo"));

        for (var hostApiIndex = 0; hostApiIndex < getCount(); hostApiIndex++)
        {
            var infoPtr = getInfo(hostApiIndex);
            if (infoPtr == IntPtr.Zero)
            {
                continue;
            }

            var type = Marshal.ReadInt32(infoPtr, TypeOffset);
            if (type == WasapiTypeId)
            {
                return hostApiIndex;
            }
        }

        logger.LogWarning("No WASAPI host API found; audio output list will include duplicate entries.");
        return null;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetHostApiCountDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetHostApiInfoDelegate(int hostApi);
}
