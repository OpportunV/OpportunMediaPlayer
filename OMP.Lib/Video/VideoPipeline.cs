using FFmpeg.AutoGen;

namespace OMP.Lib.Video;

public sealed unsafe class VideoPipeline : IDisposable
{
    public int StreamIndex { get; }

    public VideoFrame Frame { get; }

    private readonly AVCodecContext* _codecContext;
    private readonly SwsContext* _sws;
    private readonly AVFrame* _frame;

    public VideoPipeline(AVFormatContext* formatContext, int streamIndex)
    {
        StreamIndex = streamIndex;

        var stream = formatContext->streams[streamIndex];
        var codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);

        _codecContext = ffmpeg.avcodec_alloc_context3(codec);
        ffmpeg.avcodec_parameters_to_context(_codecContext, stream->codecpar);

        if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0)
            throw new ApplicationException("Could not open video codec.");

        var width = _codecContext->width;
        var height = _codecContext->height;

        _frame = ffmpeg.av_frame_alloc();

        _sws = ffmpeg.sws_getContext(
            width,
            height,
            _codecContext->pix_fmt,
            width,
            height,
            AVPixelFormat.AV_PIX_FMT_BGRA,
            ffmpeg.SWS_FAST_BILINEAR,
            null,
            null,
            null);

        var stride = width * 4;

        Frame = new VideoFrame(width, height, stride, new byte[stride * height]);
    }

    public void Enqueue(AVPacket* packet)
    {
        ffmpeg.avcodec_send_packet(_codecContext, packet);

        fixed (byte* bufferPtr = Frame.Data)
        {
            byte_ptrArray4 dstData = default;
            int_array4 dstLines = default;

            dstData[0] = bufferPtr;
            dstLines[0] = Frame.Stride;

            while (ffmpeg.avcodec_receive_frame(_codecContext, _frame) == 0)
            {
                ffmpeg.sws_scale(
                    _sws,
                    _frame->data,
                    _frame->linesize,
                    0,
                    Frame.Height,
                    dstData,
                    dstLines);
            }
        }
    }

    public void Flush()
    {
        ffmpeg.avcodec_flush_buffers(_codecContext);
    }

    public void Dispose()
    {
        fixed (AVFrame** f = &_frame)
            ffmpeg.av_frame_free(f);

        fixed (AVCodecContext** c = &_codecContext)
            ffmpeg.avcodec_free_context(c);

        ffmpeg.sws_freeContext(_sws);
    }
}