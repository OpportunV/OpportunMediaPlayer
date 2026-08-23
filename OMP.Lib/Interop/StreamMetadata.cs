using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;

namespace OMP.Lib.Interop;

internal static unsafe class StreamMetadata
{
    /// <summary>
    /// Placeholder for an absent metadata tag. Shared rather than repeated per call site because
    /// <c>AudioRouteMatcher</c> compares against it ordinally to decide a persisted preference is
    /// too vague to match on - a silent divergence there would break route restore, not fail to build.
    /// </summary>
    public const string Unknown = "Unknown";

    /// <summary>
    /// Reads a stream metadata tag, falling back to <see cref="Unknown"/> when it is absent.
    /// Takes the caller's <paramref name="logger"/> so the trace line keeps that caller's category
    /// and stays reachable through per-subsystem MinimumLevel overrides.
    /// </summary>
    public static string Read(AVStream* stream, string key, ILogger logger)
    {
        var tag = ffmpeg.av_dict_get(stream->metadata, key, null, 0);
        if (tag == null)
        {
            logger.LogTrace("Stream {StreamIndex} has no '{Key}' metadata tag.", stream->index, key);
            return Unknown;
        }

        return Marshal.PtrToStringUTF8((IntPtr)tag->value) ?? Unknown;
    }
}
