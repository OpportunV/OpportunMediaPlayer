using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OMP.Lib.Interop;

namespace OMP.Lib.Subtitle;

internal sealed unsafe class SubtitleScanner(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SubtitleScanner>();

    private static readonly HashSet<AVCodecID> _textBasedCodecs =
    [
        AVCodecID.AV_CODEC_ID_SUBRIP,
        AVCodecID.AV_CODEC_ID_ASS,
        AVCodecID.AV_CODEC_ID_SSA,
        AVCodecID.AV_CODEC_ID_WEBVTT,
        AVCodecID.AV_CODEC_ID_MOV_TEXT
    ];

    public List<SubtitleStream> GetSubtitleStreams(
        AVFormatContext* formatContext, IReadOnlyDictionary<int, Mp4TrackMetadata> mp4Tracks)
    {
        var subtitleStreams = new List<SubtitleStream>();

        for (var i = 0; i < formatContext->nb_streams; i++)
        {
            var stream = formatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_SUBTITLE)
            {
                var codecId = stream->codecpar->codec_id;
                var mp4Track = mp4Tracks.GetValueOrDefault(stream->id);
                var subtitleStream = new SubtitleStream(
                    i,
                    ffmpeg.avcodec_get_name(codecId),
                    StreamMetadata.FindTrackTitle(stream, mp4Track) ?? Missing(stream, StreamMetadata.TitleKey),
                    StreamMetadata.FindTrackLanguage(stream, mp4Track) ?? Missing(stream, StreamMetadata.LanguageKey),
                    _textBasedCodecs.Contains(codecId));

                subtitleStreams.Add(subtitleStream);
                _logger.LogDebug(
                    "Subtitle stream {StreamIndex}: {Codec}, title '{Title}', language '{Language}', text-based={IsTextBased}.",
                    subtitleStream.Id,
                    subtitleStream.Codec,
                    subtitleStream.Title,
                    subtitleStream.Language,
                    subtitleStream.IsTextBased);
            }
        }

        if (subtitleStreams.Count == 0)
        {
            _logger.LogDebug("No subtitle streams found.");
        }
        else
        {
            _logger.LogInformation("Found {Count} subtitle stream(s).", subtitleStreams.Count);
        }

        return subtitleStreams;
    }

    private string Missing(AVStream* stream, string key)
    {
        StreamMetadata.LogMissing(stream, key, _logger);
        return StreamMetadata.Unknown;
    }
}
