using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace OMP.Lib.Audio;

internal sealed unsafe class AudioScanner
{
    public List<AudioStream> GetAudioStreams(AVFormatContext* formatContext)
    {
        var audioStreams = new List<AudioStream>();

        for (var i = 0; i < formatContext->nb_streams; i++)
        {
            var stream = formatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                audioStreams.Add(
                    new AudioStream(
                        i,
                        ffmpeg.avcodec_get_name(stream->codecpar->codec_id),
                        GetMetadata(stream, "title"),
                        GetMetadata(stream, "language")));
            }
        }

        return audioStreams;
    }

    private static string GetMetadata(AVStream* stream, string key)
    {
        var tag = ffmpeg.av_dict_get(stream->metadata, key, null, 0);
        return tag != null
            ? Marshal.PtrToStringAnsi((IntPtr)tag->value) ?? "Unknown"
            : "Unknown";
    }
}