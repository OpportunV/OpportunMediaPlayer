using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OMP.Lib.Interop;

namespace OMP.Lib.Subtitle;

internal sealed unsafe class SubtitlePipeline : IDisposable
{
    public int StreamIndex { get; }

    public string ZoneId { get; }

    private readonly ILogger _logger;
    private readonly SubtitleCueStore _cueStore = new();

    private int _decodeFailures;

    private readonly AVCodecContext* _codecContext;
    private readonly Lock _decodeSync = new();

    private readonly AVRational _timeBase;

    private const double DefaultCueDurationSeconds = 4;

    public SubtitlePipeline(AVFormatContext* formatContext, int streamIndex, string zoneId,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<SubtitlePipeline>();
        StreamIndex = streamIndex;
        ZoneId = zoneId;

        var stream = formatContext->streams[streamIndex];
        var codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);
        if (codec == null)
        {
            _logger.LogError(
                "No decoder available for subtitle stream {StreamIndex} (codec {Codec}).",
                streamIndex,
                ffmpeg.avcodec_get_name(stream->codecpar->codec_id));
            throw new ApplicationException("Could not find subtitle decoder.");
        }

        _timeBase = stream->time_base;
        _codecContext = ffmpeg.avcodec_alloc_context3(codec);
        ffmpeg.avcodec_parameters_to_context(_codecContext, stream->codecpar);

        var openResult = ffmpeg.avcodec_open2(_codecContext, codec, null);
        if (openResult < 0)
        {
            _logger.LogError(
                "Failed to open subtitle codec {Codec} for stream {StreamIndex}: {Error}.",
                ffmpeg.avcodec_get_name(stream->codecpar->codec_id),
                streamIndex,
                FFmpegError.Describe(openResult));
            throw new ApplicationException("Could not open subtitle codec.");
        }

        _logger.LogDebug(
            "Subtitle pipeline built: stream {StreamIndex} -> zone '{ZoneId}'.",
            streamIndex,
            zoneId);
    }

    public void Dispose()
    {
        if (_decodeFailures > 0)
        {
            _logger.LogWarning(
                "Subtitle stream {StreamIndex}: {Count} decode failure(s).",
                StreamIndex,
                _decodeFailures);
        }

        fixed (AVCodecContext** codec = &_codecContext)
        {
            ffmpeg.avcodec_free_context(codec);
        }
    }

    public void Enqueue(AVPacket* packet)
    {
        AVSubtitle subtitle;
        int gotSubtitle;
        int usedBytes;

        lock (_decodeSync)
        {
            usedBytes = ffmpeg.avcodec_decode_subtitle2(_codecContext, &subtitle, &gotSubtitle, packet);
        }

        if (usedBytes < 0)
        {
            if (Interlocked.Increment(ref _decodeFailures) == 1)
            {
                _logger.LogWarning(
                    "Subtitle stream {StreamIndex}: decode failed: {Error}. " +
                    "Further occurrences are counted and reported on close.",
                    StreamIndex,
                    FFmpegError.Describe(usedBytes));
            }

            return;
        }

        if (gotSubtitle == 0)
        {
            return;
        }

        try
        {
            var cue = BuildCue(packet, subtitle);
            if (cue is not null)
            {
                _cueStore.Add(cue);
            }
        }
        finally
        {
            ffmpeg.avsubtitle_free(&subtitle);
        }
    }

    public IReadOnlyList<SubtitleCue> GetActiveCues(double timeSeconds)
    {
        return _cueStore.GetActive(timeSeconds);
    }

    public void Flush()
    {
        lock (_decodeSync)
        {
            ffmpeg.avcodec_flush_buffers(_codecContext);
        }
    }

    private SubtitleCue? BuildCue(AVPacket* packet, AVSubtitle subtitle)
    {
        var baseSeconds = subtitle.pts != ffmpeg.AV_NOPTS_VALUE
            ? subtitle.pts / (double)ffmpeg.AV_TIME_BASE
            : packet->pts * ffmpeg.av_q2d(_timeBase);

        var startSeconds = baseSeconds + subtitle.start_display_time / 1000.0;

        double endSeconds;
        if (packet->duration > 0)
        {
            endSeconds = startSeconds + packet->duration * ffmpeg.av_q2d(_timeBase);
        }
        else if (subtitle.end_display_time > 0)
        {
            endSeconds = baseSeconds + subtitle.end_display_time / 1000.0;
        }
        else
        {
            endSeconds = startSeconds + DefaultCueDurationSeconds;
        }

        var lines = new List<SubtitleLine>();
        for (var i = 0; i < subtitle.num_rects; i++)
        {
            var rect = subtitle.rects[i];
            if (rect == null)
            {
                continue;
            }

            var rawText = ExtractRectText(rect);
            if (rawText is not null)
            {
                lines.AddRange(SubtitleTextParser.Parse(rawText));
            }
        }

        return lines.Any(line => line.Runs.Count > 0)
            ? new SubtitleCue(ZoneId, lines, startSeconds, endSeconds)
            : null;
    }

    private static string? ExtractRectText(AVSubtitleRect* rect)
    {
        if (rect->type == AVSubtitleType.SUBTITLE_ASS && rect->ass != null)
        {
            var assLine = Marshal.PtrToStringUTF8((IntPtr)rect->ass) ?? string.Empty;
            return ExtractAssDialogueText(assLine);
        }

        if (rect->type == AVSubtitleType.SUBTITLE_TEXT && rect->text != null)
        {
            return Marshal.PtrToStringUTF8((IntPtr)rect->text);
        }

        return null;
    }

    private static string ExtractAssDialogueText(string assLine)
    {
        var fields = assLine.Split(',', 9);
        return fields.Length == 9 ? fields[8] : assLine;
    }
}
