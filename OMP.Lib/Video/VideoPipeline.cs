using System.Runtime.InteropServices;
using System.Threading.Channels;
using FFmpeg.AutoGen;
using OMP.Lib.Extensions;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OMP.Lib.Video;

internal sealed unsafe class VideoPipeline : IDisposable
{
    public int StreamIndex { get; }
    public double DecodeFps { get; private set; }

    private readonly VideoFrame _baseVideoFrame;
    private readonly AVCodecContext* _codecContext;
    private readonly CancellationToken _cancellationToken;
    private readonly AVFrame* _frame;
    private readonly nint[] _frameBuffers;
    private int _nextFrameBufferIndex;

    private readonly Channel<VideoFrame> _frameChannel = Channel.CreateBounded<VideoFrame>(
        new BoundedChannelOptions(BufferedFrameCount)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

    private readonly SwsContext* _sws;
    private readonly AVRational _timeBase;
    private int _decodedFrames;
    private readonly Stopwatch _decodeFpsStopwatch = Stopwatch.StartNew();
    private const int BufferedFrameCount = 8;

    public VideoPipeline(AVFormatContext* formatContext, int streamIndex, CancellationToken cancellationToken)
    {
        StreamIndex = streamIndex;
        _cancellationToken = cancellationToken;

        var stream = formatContext->streams[streamIndex];
        _timeBase = stream->time_base;
        var codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);

        _codecContext = ffmpeg.avcodec_alloc_context3(codec);
        ffmpeg.avcodec_parameters_to_context(_codecContext, stream->codecpar);
        _codecContext->thread_count = Environment.ProcessorCount;
        _codecContext->thread_type = ffmpeg.FF_THREAD_FRAME;

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
        var frameBufferSize = stride * height;
        _frameBuffers = new nint[BufferedFrameCount];
        for (var i = 0; i < BufferedFrameCount; i++)
        {
            _frameBuffers[i] = Marshal.AllocHGlobal(frameBufferSize);
        }

        _baseVideoFrame = new VideoFrame(width, height, stride, 0, frameBufferSize, 0);
    }

    public void Dispose()
    {
        Flush();
        _frameChannel.Writer.Complete();
        foreach (var frameBuffer in _frameBuffers)
        {
            Marshal.FreeHGlobal(frameBuffer);
        }

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

    public bool TryPeek(out VideoFrame videoFrame)
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
            var buffer = _frameBuffers[_nextFrameBufferIndex];
            _nextFrameBufferIndex = (_nextFrameBufferIndex + 1) % _frameBuffers.Length;

            byte_ptrArray4 dstData = default;
            int_array4 dstLines = default;

            dstData[0] = (byte*)buffer;
            dstLines[0] = _baseVideoFrame.Stride;

            ffmpeg.sws_scale(
                _sws,
                _frame->data,
                _frame->linesize,
                0,
                _baseVideoFrame.Height,
                dstData,
                dstLines);

            var videoFrame = _baseVideoFrame with { DataPtr = buffer, TimeSeconds = time };
            if (!_frameChannel.Writer.TryWriteBlocking(videoFrame, _cancellationToken))
            {
                break;
            }

            _decodedFrames++;

            if (_decodeFpsStopwatch.ElapsedMilliseconds >= 1000)
            {
                DecodeFps = _decodedFrames * 1000.0 / _decodeFpsStopwatch.ElapsedMilliseconds;
                _decodedFrames = 0;
                _decodeFpsStopwatch.Restart();
            }
        }
    }

    public void Flush()
    {
        ffmpeg.avcodec_flush_buffers(_codecContext);
        while (_frameChannel.Reader.TryRead(out _))
        {
        }

        _nextFrameBufferIndex = 0;
    }
}