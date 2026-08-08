using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace OMP.Lib.Interop;

internal static unsafe class FFmpegError
{
    private const int BufferSize = 256;

    public static bool IsRetryOrEof(int error)
    {
        return error == ffmpeg.AVERROR(ffmpeg.EAGAIN) || error == ffmpeg.AVERROR_EOF;
    }

    public static string Describe(int error)
    {
        var buffer = stackalloc byte[BufferSize];
        return ffmpeg.av_strerror(error, buffer, BufferSize) == 0
            ? $"{error} ({Marshal.PtrToStringAnsi((IntPtr)buffer)})"
            : error.ToString();
    }
}
