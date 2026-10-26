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

    public const string TitleKey = "title";
    public const string LanguageKey = "language";

    private const string HandlerNameKey = "handler_name";

    /// <summary>
    /// The title a track gives itself, or null: its own <c>title</c> tag, then an MP4 track's
    /// <c>udta/name</c> (<paramref name="mp4Track"/>, which FFmpeg doesn't read), then a
    /// <c>handler_name</c> that is a real name rather than a muxer default. Shared by the audio and
    /// subtitle scanners so both kinds of track are labelled the same way.
    /// </summary>
    public static string? FindTrackTitle(AVStream* stream, Mp4TrackMetadata? mp4Track)
    {
        var handlerName = Find(stream->metadata, HandlerNameKey);
        return Find(stream->metadata, TitleKey)
            ?? mp4Track?.Name
            ?? (HandlerName.IsTrackName(handlerName) ? handlerName : null);
    }

    /// <summary>
    /// An MP4 track's full <c>elng</c> tag when it has one (FFmpeg only reads the 3-letter <c>mdhd</c>
    /// code, which can't say <c>en-US</c> or <c>pt-BR</c>), otherwise the track's own <c>language</c> tag.
    /// </summary>
    public static string? FindTrackLanguage(AVStream* stream, Mp4TrackMetadata? mp4Track) =>
        mp4Track?.Language ?? Find(stream->metadata, LanguageKey);

    /// <summary>
    /// Reads a tag from any metadata dictionary (a stream's or the whole container's), or null when
    /// it is absent - for callers that chain several fallback sources before settling on <see cref="Unknown"/>.
    /// </summary>
    public static string? Find(AVDictionary* metadata, string key)
    {
        var tag = ffmpeg.av_dict_get(metadata, key, null, 0);
        return tag == null ? null : Marshal.PtrToStringUTF8((IntPtr)tag->value);
    }

    public static void LogMissing(AVStream* stream, string key, ILogger logger) =>
        logger.LogTrace("Stream {StreamIndex} has no '{Key}' metadata tag.", stream->index, key);
}
