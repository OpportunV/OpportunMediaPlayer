using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PortAudioStream = PortAudioSharp.Stream;

namespace OMP.Lib.Interop;

internal static class PortAudioStreamInfo
{
    private static readonly FieldInfo? _streamPtrField =
        typeof(PortAudioStream).GetField("streamPtr", BindingFlags.NonPublic | BindingFlags.Instance);

    private const int InputLatencyOffset = 8;
    private const int OutputLatencyOffset = 16;
    private const int SampleRateOffset = 24;

    public static bool TryGetNegotiatedOutputLatencySeconds(
        PortAudioStream stream, ILogger logger, out double outputLatencySeconds)
    {
        if (_streamPtrField?.GetValue(stream) is not IntPtr streamPtr || streamPtr == IntPtr.Zero)
        {
            logger.LogDebug("Could not resolve the native PortAudio stream handle to report negotiated latency.");
            outputLatencySeconds = 0;
            return false;
        }

        if (!NativeLibrary.TryLoad("portaudio", out var handle))
        {
            outputLatencySeconds = 0;
            return false;
        }

        var getStreamInfo = Marshal.GetDelegateForFunctionPointer<GetStreamInfoDelegate>(
            NativeLibrary.GetExport(handle, "Pa_GetStreamInfo"));

        var infoPtr = getStreamInfo(streamPtr);
        if (infoPtr == IntPtr.Zero)
        {
            logger.LogDebug("PortAudio returned no stream info; cannot report negotiated latency.");
            outputLatencySeconds = 0;
            return false;
        }

        var inputLatencySeconds = ReadDouble(infoPtr, InputLatencyOffset);
        var sampleRate = ReadDouble(infoPtr, SampleRateOffset);
        outputLatencySeconds = ReadDouble(infoPtr, OutputLatencyOffset);

        logger.LogDebug(
            "PortAudio negotiated stream: outputLatency={OutputLatencyMs:F1}ms, " +
            "inputLatency={InputLatencyMs:F1}ms, sampleRate={SampleRate}Hz.",
            outputLatencySeconds * 1000,
            inputLatencySeconds * 1000,
            sampleRate);

        return true;
    }

    private static double ReadDouble(IntPtr ptr, int offset) =>
        BitConverter.Int64BitsToDouble(Marshal.ReadInt64(ptr, offset));

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetStreamInfoDelegate(IntPtr stream);
}
