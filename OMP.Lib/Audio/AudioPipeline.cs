using System.Threading.Channels;
using FFmpeg.AutoGen;
using NAudio.Wave;
using OMP.Lib.Extensions;

namespace OMP.Lib.Audio;

internal sealed unsafe class AudioPipeline : IDisposable
{
    public int StreamIndex { get; }

    private double _currentTimeSeconds;
    private double _speed = 1.0;
    private readonly AudioSpeedProcessor _speedProcessor = new();

    private readonly Channel<AudioChunk> _decodedPcmChannel = Channel.CreateBounded<AudioChunk>(
        new BoundedChannelOptions(128)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

    private readonly AVCodecContext* _codecContext;
    private readonly AVFrame* _frame;
    private readonly BufferedWaveProvider _buffer;

    private readonly CancellationToken _cancellationToken;
    private readonly byte[] _managedBuffer = new byte[8192];
    private readonly WaveOutEvent _output;
    private readonly SwrContext* _swr;
    private readonly AVRational _timeBase;

    private const int OutputSampleRate = 44100;
    private const int OutputBitsPerSample = 16;
    private const int OutputChannelCount = 2;
    private const int BytesPerSampleFrame = OutputChannelCount * OutputBitsPerSample / 8;
    private const int MaxResampledSamplesPerConvert = 4096;
    private const int BufferDurationSeconds = 2;
    private const double PumpWindowSeconds = 0.2;
    private const double BufferHighWaterMarkRatio = 0.9;

    public AudioPipeline(AVFormatContext* formatContext, int streamIndex, int deviceIndex,
        CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        StreamIndex = streamIndex;

        var stream = formatContext->streams[streamIndex];
        var codec = ffmpeg.avcodec_find_decoder(stream->codecpar->codec_id);

        _timeBase = stream->time_base;
        _codecContext = ffmpeg.avcodec_alloc_context3(codec);
        ffmpeg.avcodec_parameters_to_context(_codecContext, stream->codecpar);
        if (ffmpeg.avcodec_open2(_codecContext, codec, null) < 0)
        {
            throw new ApplicationException("Could not open codec.");
        }

        _frame = ffmpeg.av_frame_alloc();

        _swr = ffmpeg.swr_alloc();

        AVChannelLayout outLayout;
        ffmpeg.av_channel_layout_default(&outLayout, OutputChannelCount);

        ffmpeg.av_opt_set_chlayout(_swr, "out_chlayout", &outLayout, 0);
        ffmpeg.av_opt_set_int(_swr, "out_sample_rate", OutputSampleRate, 0);
        ffmpeg.av_opt_set_sample_fmt(_swr, "out_sample_fmt", AVSampleFormat.AV_SAMPLE_FMT_S16, 0);

        ffmpeg.av_opt_set_chlayout(_swr, "in_chlayout", &_codecContext->ch_layout, 0);
        ffmpeg.av_opt_set_int(_swr, "in_sample_rate", _codecContext->sample_rate, 0);
        ffmpeg.av_opt_set_sample_fmt(_swr, "in_sample_fmt", _codecContext->sample_fmt, 0);

        if (ffmpeg.swr_init(_swr) < 0)
        {
            throw new ApplicationException("Could not initialize resampler.");
        }

        var waveFormat = new WaveFormat(OutputSampleRate, OutputBitsPerSample, OutputChannelCount);

        _buffer = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(BufferDurationSeconds),
            DiscardOnBufferOverflow = false
        };

        _output = new WaveOutEvent { DeviceNumber = deviceIndex };
        _output.Init(_buffer);
    }

    public void Dispose()
    {
        _output.Dispose();

        fixed (AVFrame** frame = &_frame)
        {
            ffmpeg.av_frame_free(frame);
        }

        fixed (AVCodecContext** codec = &_codecContext)
        {
            ffmpeg.avcodec_free_context(codec);
        }

        fixed (SwrContext** swr = &_swr)
        {
            ffmpeg.swr_free(swr);
        }
    }

    public void Enqueue(AVPacket* packet)
    {
        ffmpeg.avcodec_send_packet(_codecContext, packet);
        fixed (byte* outPtr = _managedBuffer)
        {
            var outPtrs = stackalloc byte*[1];
            outPtrs[0] = outPtr;

            while (ffmpeg.avcodec_receive_frame(_codecContext, _frame) == 0 &&
                   !_cancellationToken.IsCancellationRequested)
            {
                if (_frame->best_effort_timestamp != ffmpeg.AV_NOPTS_VALUE)
                {
                    _currentTimeSeconds = _frame->best_effort_timestamp * ffmpeg.av_q2d(_timeBase);
                }

                var dstSamples = ffmpeg.swr_convert(
                    _swr,
                    outPtrs,
                    MaxResampledSamplesPerConvert,
                    _frame->extended_data,
                    _frame->nb_samples);

                if (dstSamples <= 0)
                {
                    continue;
                }

                var outBytes = dstSamples * BytesPerSampleFrame;
                var speedAdjustedBytes = _speedProcessor.Process(_managedBuffer, outBytes, _speed, OutputSampleRate);

                var chunkBytes = new byte[speedAdjustedBytes];
                Buffer.BlockCopy(_speedProcessor.AdjustedBuffer, 0, chunkBytes, 0, speedAdjustedBytes);
                var chunk = new AudioChunk(chunkBytes, speedAdjustedBytes, _currentTimeSeconds);
                if (!_decodedPcmChannel.Writer.TryWriteBlocking(chunk, _cancellationToken))
                {
                    break;
                }
            }
        }
    }

    public void PumpToOutput(double targetMediaTimeSeconds)
    {
        while (_decodedPcmChannel.Reader.TryPeek(out var chunk))
        {
            if (chunk.TimeSeconds < targetMediaTimeSeconds - PumpWindowSeconds)
            {
                _decodedPcmChannel.Reader.TryRead(out _);
                continue;
            }

            if (chunk.TimeSeconds > targetMediaTimeSeconds + PumpWindowSeconds)
            {
                break;
            }

            if (_buffer.BufferedBytes > _buffer.BufferLength * BufferHighWaterMarkRatio)
            {
                break;
            }

            var remainingBufferCapacity = _buffer.BufferLength - _buffer.BufferedBytes;
            if (chunk.Length > remainingBufferCapacity)
            {
                break;
            }

            _decodedPcmChannel.Reader.TryRead(out chunk);
            try
            {
                _buffer.AddSamples(chunk.Data, 0, chunk.Length);
            }
            catch (InvalidOperationException)
            {
                break;
            }
        }
    }

    public void DiscardBefore(double timeSeconds)
    {
        while (_decodedPcmChannel.Reader.TryPeek(out var chunk))
        {
            if (chunk.TimeSeconds >= timeSeconds)
            {
                break;
            }

            _decodedPcmChannel.Reader.TryRead(out _);
        }
    }

    public void Play()
    {
        _output.Play();
    }

    public void Pause()
    {
        _output.Pause();
    }

    public void Flush()
    {
        ffmpeg.avcodec_flush_buffers(_codecContext);
        ffmpeg.swr_close(_swr);
        ffmpeg.swr_init(_swr);

        _buffer.ClearBuffer();
        while (_decodedPcmChannel.Reader.TryRead(out _))
        {
        }
    }

    public void ResetClock(double timeSeconds)
    {
        _currentTimeSeconds = Math.Max(0, timeSeconds);
    }

    public void SetSpeed(double speed)
    {
        _speed = speed;
    }
}
