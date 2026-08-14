using System.Runtime.InteropServices;
using System.Threading.Channels;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OMP.Lib.Extensions;
using OMP.Lib.Interop;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OMP.Lib.Video;

internal sealed unsafe class VideoPipeline : IDisposable
{
    public int StreamIndex { get; }
    public double DecodeFps { get; private set; }

    public int DiagFrameQueueCount => _frameChannel.Reader.Count;

    private readonly ILogger _logger;
    private int _sendPacketFailures;

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
    private readonly Lock _decodeSync = new();
    private int _decodedFrames;
    private readonly Stopwatch _decodeFpsStopwatch = Stopwatch.StartNew();
    private const int BufferedFrameCount = 8;

    private const int FrameBufferPoolSize = BufferedFrameCount * 2;

    public VideoPipeline(AVFormatContext* formatContext, int streamIndex, CancellationToken cancellationToken,
        ILoggerFactory loggerFactory)
    {
        StreamIndex = streamIndex;
        _cancellationToken = cancellationToken;
        _logger = loggerFactory.CreateLogger<VideoPipeline>();

        var stream = formatContext->streams[streamIndex];
        _timeBase = stream->time_base;
        var codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);
        if (codec == null)
        {
            _logger.LogError(
                "No decoder available for video stream {StreamIndex} (codec {Codec}).",
                streamIndex,
                ffmpeg.avcodec_get_name(stream->codecpar->codec_id));
            throw new ApplicationException("Could not find video decoder.");
        }

        _codecContext = ffmpeg.avcodec_alloc_context3(codec);
        ffmpeg.avcodec_parameters_to_context(_codecContext, stream->codecpar);
        _codecContext->thread_count = Environment.ProcessorCount;
        _codecContext->thread_type = ffmpeg.FF_THREAD_FRAME;

        var openResult = ffmpeg.avcodec_open2(_codecContext, codec, null);
        if (openResult < 0)
        {
            _logger.LogError(
                "Failed to open video codec {Codec} for stream {StreamIndex}: {Error}.",
                ffmpeg.avcodec_get_name(stream->codecpar->codec_id),
                streamIndex,
                FFmpegError.Describe(openResult));
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

        if (_sws == null)
        {
            _logger.LogError(
                "Failed to create scaler for video stream {StreamIndex} ({Width}x{Height}, {PixelFormat} -> BGRA).",
                streamIndex,
                width,
                height,
                _codecContext->pix_fmt);
            throw new ApplicationException("Could not create video scaler.");
        }

        var stride = width * 4;
        var frameBufferSize = stride * height;
        _frameBuffers = new nint[FrameBufferPoolSize];
        for (var i = 0; i < FrameBufferPoolSize; i++)
        {
            _frameBuffers[i] = Marshal.AllocHGlobal(frameBufferSize);
        }

        _baseVideoFrame = new VideoFrame(width, height, stride, 0, frameBufferSize, 0);

        _logger.LogDebug(
            "Video pipeline built: stream {StreamIndex}, {Width}x{Height}, {PixelFormat}, {ThreadCount} thread(s).",
            streamIndex,
            width,
            height,
            _codecContext->pix_fmt,
            _codecContext->thread_count);
    }

    public void Dispose()
    {
        if (_sendPacketFailures > 0)
        {
            _logger.LogWarning(
                "Video stream {StreamIndex}: {Count} decode submission(s) failed.",
                StreamIndex,
                _sendPacketFailures);
        }

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
        int sendResult;
        lock (_decodeSync)
        {
            sendResult = ffmpeg.avcodec_send_packet(_codecContext, packet);
        }

        if (sendResult < 0 && !FFmpegError.IsRetryOrEof(sendResult))
        {
            if (Interlocked.Increment(ref _sendPacketFailures) == 1)
            {
                _logger.LogWarning(
                    "Video stream {StreamIndex}: decode submission failed: {Error}. " +
                    "Further occurrences are counted and reported on close.",
                    StreamIndex,
                    FFmpegError.Describe(sendResult));
            }
        }

        while (true)
        {
            int receiveResult;
            lock (_decodeSync)
            {
                receiveResult = ffmpeg.avcodec_receive_frame(_codecContext, _frame);
            }

            if (receiveResult != 0)
            {
                break;
            }

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
        lock (_decodeSync)
        {
            ffmpeg.avcodec_flush_buffers(_codecContext);
        }

        while (_frameChannel.Reader.TryRead(out _))
        {
        }

        _nextFrameBufferIndex = 0;
    }
}