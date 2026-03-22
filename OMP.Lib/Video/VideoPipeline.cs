using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using FFmpeg.AutoGen;
using OMP.Lib.Extensions;

namespace OMP.Lib.Video;

public sealed unsafe class VideoPipeline : IDisposable
{
    public int StreamIndex { get; }

    private readonly VideoFrame _baseVideoFrame;
    private readonly AVCodecContext* _codecContext;
    private readonly AVFrame* _frame;

    private readonly Channel<VideoFrame> _frameChannel = Channel.CreateBounded<VideoFrame>(
        new BoundedChannelOptions(3)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly SwsContext* _sws;
    private readonly AVRational _timeBase;

    public VideoPipeline(AVFormatContext* formatContext, int streamIndex)
    {
        StreamIndex = streamIndex;

        var stream = formatContext->streams[streamIndex];
        _timeBase = stream->time_base;
        var codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);

        _codecContext = ffmpeg.avcodec_alloc_context3(codec);
        ffmpeg.avcodec_parameters_to_context(_codecContext, stream->codecpar);

        if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0)
        {
            throw new ApplicationException("Could not open video codec.");
        }

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

        _baseVideoFrame = new VideoFrame(width, height, stride, [], 0);
    }

    public void Dispose()
    {
        Flush();
        _frameChannel.Writer.Complete();
        fixed (AVFrame** f = &_frame)
        {
            ffmpeg.av_frame_free(f);
        }

        fixed (AVCodecContext** c = &_codecContext)
        {
            ffmpeg.avcodec_free_context(c);
        }

        ffmpeg.sws_freeContext(_sws);
    }

    public bool TryPeek([NotNullWhen(true)] out VideoFrame? videoFrame)
    {
        return _frameChannel.Reader.TryPeek(out videoFrame);
    }

    public void Pop()
    {
        _frameChannel.Reader.TryRead(out _);
    }

    public void Enqueue(AVPacket* packet)
    {
        ffmpeg.avcodec_send_packet(_codecContext, packet);

        while (ffmpeg.avcodec_receive_frame(_codecContext, _frame) == 0)
        {
            if (_frame->best_effort_timestamp == ffmpeg.AV_NOPTS_VALUE)
            {
                continue;
            }

            var time = _frame->best_effort_timestamp * ffmpeg.av_q2d(_timeBase);
            var buffer = new byte[_baseVideoFrame.Stride * _baseVideoFrame.Height];

            fixed (byte* bufferPtr = buffer)
            {
                byte_ptrArray4 dstData = default;
                int_array4 dstLines = default;

                dstData[0] = bufferPtr;
                dstLines[0] = _baseVideoFrame.Stride;

                ffmpeg.sws_scale(
                    _sws,
                    _frame->data,
                    _frame->linesize,
                    0,
                    _baseVideoFrame.Height,
                    dstData,
                    dstLines);
            }

            var videoFrame = _baseVideoFrame with { Data = buffer, TimeSeconds = time };
            _frameChannel.Writer.Write(videoFrame);
        }
    }

    public void Flush()
    {
        ffmpeg.avcodec_flush_buffers(_codecContext);
        while (_frameChannel.Reader.TryRead(out _))
        {
        }
    }
}