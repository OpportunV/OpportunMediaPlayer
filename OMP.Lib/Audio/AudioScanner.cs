using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OMP.Lib.Interop;

namespace OMP.Lib.Audio;

internal sealed unsafe class AudioScanner(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AudioScanner>();

    public List<AudioStream> GetAudioStreams(AVFormatContext* formatContext)
    {
        var audioStreams = new List<AudioStream>();

        for (var i = 0; i < formatContext->nb_streams; i++)
        {
            var stream = formatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                var audioStream = new AudioStream(
                    i,
                    ffmpeg.avcodec_get_name(stream->codecpar->codec_id),
                    StreamMetadata.Read(stream, "title", _logger),
                    StreamMetadata.Read(stream, "language", _logger));

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
}
