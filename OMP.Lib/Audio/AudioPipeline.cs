using FFmpeg.AutoGen;
using NAudio.Wave;

namespace OMP.Lib.Audio;

public sealed unsafe class AudioPipeline : IDisposable
{
    public double CurrentTimeSeconds { get; private set; }

    public int StreamIndex { get; }

    private readonly BufferedWaveProvider _buffer;
    private readonly byte[] _managedBuffer = new byte[8192];
    private readonly AVCodecContext* _codecContext;
    private readonly AVFrame* _frame;

    private readonly CancellationToken _cancellationToken;
    private readonly WaveOutEvent _output;
    private readonly SwrContext* _swr;
    private readonly AVRational _timeBase;

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
        ffmpeg.av_channel_layout_default(&outLayout, 2);

        ffmpeg.av_opt_set_chlayout(_swr, "out_chlayout", &outLayout, 0);
        ffmpeg.av_opt_set_int(_swr, "out_sample_rate", 44100, 0);
        ffmpeg.av_opt_set_sample_fmt(_swr, "out_sample_fmt", AVSampleFormat.AV_SAMPLE_FMT_S16, 0);

        ffmpeg.av_opt_set_chlayout(_swr, "in_chlayout", &_codecContext->ch_layout, 0);
        ffmpeg.av_opt_set_int(_swr, "in_sample_rate", _codecContext->sample_rate, 0);
        ffmpeg.av_opt_set_sample_fmt(_swr, "in_sample_fmt", _codecContext->sample_fmt, 0);

        if (ffmpeg.swr_init(_swr) < 0)
        {
            throw new ApplicationException("Could not initialize resampler.");
        }

        var waveFormat = new WaveFormat(44100, 16, 2);

        _buffer = new BufferedWaveProvider(waveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(2),
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
                    CurrentTimeSeconds = _frame->best_effort_timestamp * ffmpeg.av_q2d(_timeBase);
                }

                var dstSamples = ffmpeg.swr_convert(
                    _swr,
                    outPtrs,
                    4096,
                    _frame->extended_data,
                    _frame->nb_samples);

                if (dstSamples <= 0)
                {
                    continue;
                }

                var outBytes = dstSamples * 2 * 2;

                while (_buffer.BufferedBytes > _buffer.BufferLength * 0.75 &&
                       !_cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(1);
                }

                _buffer.AddSamples(_managedBuffer, 0, outBytes);
            }
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
    }
}