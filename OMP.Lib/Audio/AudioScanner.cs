using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OMP.Lib.Interop;

namespace OMP.Lib.Audio;

internal sealed unsafe class AudioScanner(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AudioScanner>();

    private const string TitleKey = "title";
    private const string LanguageKey = "language";
    private const string HandlerNameKey = "handler_name";

    public List<AudioStream> GetAudioStreams(AVFormatContext* formatContext)
    {
        var audioStreams = new List<AudioStream>();
        var useContainerTags = DescribesSingleAudioTrack(formatContext);

        for (var i = 0; i < formatContext->nb_streams; i++)
        {
            var stream = formatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                var audioStream = new AudioStream(
                    i,
                    ffmpeg.avcodec_get_name(stream->codecpar->codec_id),
                    ReadTitle(formatContext, stream, useContainerTags),
                    ReadLanguage(formatContext, stream, useContainerTags));

                audioStreams.Add(audioStream);
                _logger.LogDebug(
                    "Audio stream {StreamIndex}: {Codec}, title '{Title}', language '{Language}'.",
                    audioStream.Id,
                    audioStream.Codec,
                    audioStream.Title,
                    audioStream.Language);
            }
        }

        if (audioStreams.Count == 0)
        {
            _logger.LogWarning("No audio streams found.");
        }
        else
        {
            _logger.LogInformation("Found {Count} audio stream(s).", audioStreams.Count);
        }

        return audioStreams;
    }

    /// <summary>
    /// Audio-only formats (mp3's ID3, flac's Vorbis comments, wav's INFO chunk) keep their tags on the
    /// container. A video file's container title is the movie's name,
    /// and with several audio tracks there's no telling which one it would describe.
    /// </summary>
    private static bool DescribesSingleAudioTrack(AVFormatContext* formatContext)
    {
        var audioCount = 0;
        for (var i = 0; i < formatContext->nb_streams; i++)
        {
            var stream = formatContext->streams[i];
            switch (stream->codecpar->codec_type)
            {
                case AVMediaType.AVMEDIA_TYPE_AUDIO:
                    audioCount++;
                    break;
                case AVMediaType.AVMEDIA_TYPE_VIDEO when (stream->disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) == 0:
                    return false;
            }
        }

        return audioCount == 1;
    }

    private string ReadTitle(AVFormatContext* formatContext, AVStream* stream, bool useContainerTags)
    {
        var handlerName = StreamMetadata.Find(stream->metadata, HandlerNameKey);
        var title = StreamMetadata.Find(stream->metadata, TitleKey)
            ?? (HandlerName.IsTrackName(handlerName) ? handlerName : null)
            ?? (useContainerTags ? StreamMetadata.Find(formatContext->metadata, TitleKey) : null);

        return title ?? Missing(stream, TitleKey);
    }

    private string ReadLanguage(AVFormatContext* formatContext, AVStream* stream, bool useContainerTags)
    {
        var language = StreamMetadata.Find(stream->metadata, LanguageKey)
            ?? (useContainerTags ? StreamMetadata.Find(formatContext->metadata, LanguageKey) : null);

        return language ?? Missing(stream, LanguageKey);
    }

    private string Missing(AVStream* stream, string key)
    {
        StreamMetadata.LogMissing(stream, key, _logger);
        return StreamMetadata.Unknown;
    }
}
