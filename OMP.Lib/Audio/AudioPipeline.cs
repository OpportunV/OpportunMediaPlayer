using System.Threading.Channels;
using FFmpeg.AutoGen;
using NAudio.Dsp;
using NAudio.Wave;
using OMP.Lib.Extensions;

namespace OMP.Lib.Audio;

public sealed unsafe class AudioPipeline : IDisposable
{
    public int StreamIndex { get; }

    private double _currentTimeSeconds;
    private byte[] _speedAdjustedBuffer = new byte[8192];
    private double _speed = 1.0;
    private float[] _pitchLeftBuffer = [];
    private float[] _pitchRightBuffer = [];

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
    private readonly SmbPitchShifter _leftPitchShifter = new();
    private readonly SmbPitchShifter _rightPitchShifter = new();

    private const int BytesPerSampleFrame = 4;
    private const int OutputSampleRate = 44100;
    private const int PitchFftFrameSize = 1024;
    private const int PitchOversampling = 8;

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
                    _currentTimeSeconds = _frame->best_effort_timestamp * ffmpeg.av_q2d(_timeBase);
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
                var speedAdjustedBytes = AdjustPcmSpeed(_managedBuffer, outBytes);
                ApplyPitchPreservingStretch(_speedAdjustedBuffer, speedAdjustedBytes);

                var chunkBytes = new byte[speedAdjustedBytes];
                Buffer.BlockCopy(_speedAdjustedBuffer, 0, chunkBytes, 0, speedAdjustedBytes);
                var chunk = new AudioChunk(chunkBytes, speedAdjustedBytes, _currentTimeSeconds);
                _decodedPcmChannel.Writer.Write(chunk, _cancellationToken);
            }
        }
    }

    public void PumpToOutput(double targetMediaTimeSeconds)
    {
        while (_decodedPcmChannel.Reader.TryPeek(out var chunk))
        {
            if (chunk.TimeSeconds > targetMediaTimeSeconds + 0.2)
            {
                break;
            }

            if (_buffer.BufferedBytes > _buffer.BufferLength * 0.9)
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

    private int AdjustPcmSpeed(byte[] source, int sourceBytes)
    {
        var speed = _speed;
        if (Math.Abs(speed - 1.0) < 0.001)
        {
            EnsureSpeedBufferCapacity(sourceBytes);
            Buffer.BlockCopy(source, 0, _speedAdjustedBuffer, 0, sourceBytes);
            return sourceBytes;
        }

        var sourceFrames = sourceBytes / BytesPerSampleFrame;
        if (sourceFrames == 0)
        {
            return 0;
        }

        var outputFrames = Math.Max(1, (int)Math.Round(sourceFrames / speed));
        var outputBytes = outputFrames * BytesPerSampleFrame;
        EnsureSpeedBufferCapacity(outputBytes);

        for (var i = 0; i < outputFrames; i++)
        {
            var sourceFrame = (int)Math.Min(sourceFrames - 1, Math.Floor(i * speed));
            Buffer.BlockCopy(
                source,
                sourceFrame * BytesPerSampleFrame,
                _speedAdjustedBuffer,
                i * BytesPerSampleFrame,
                BytesPerSampleFrame);
        }

        return outputBytes;
    }

    private void EnsureSpeedBufferCapacity(int requiredSize)
    {
        if (_speedAdjustedBuffer.Length >= requiredSize)
        {
            return;
        }

        _speedAdjustedBuffer = new byte[requiredSize];
    }

    private void ApplyPitchPreservingStretch(byte[] pcmBuffer, int length)
    {
        if (Math.Abs(_speed - 1.0) < 0.001)
        {
            return;
        }

        var frames = length / BytesPerSampleFrame;
        if (frames < 64)
        {
            return;
        }

        if (_pitchLeftBuffer.Length < frames)
        {
            _pitchLeftBuffer = new float[frames];
            _pitchRightBuffer = new float[frames];
        }

        for (var i = 0; i < frames; i++)
        {
            var offset = i * BytesPerSampleFrame;
            _pitchLeftBuffer[i] = BitConverter.ToInt16(pcmBuffer, offset) / 32768f;
            _pitchRightBuffer[i] = BitConverter.ToInt16(pcmBuffer, offset + 2) / 32768f;
        }

        var pitchShift = (float)Math.Clamp(1.0 / _speed, 0.5, 2.0);
        _leftPitchShifter.PitchShift(
            pitchShift,
            frames,
            PitchFftFrameSize,
            PitchOversampling,
            OutputSampleRate,
            _pitchLeftBuffer);
        _rightPitchShifter.PitchShift(
            pitchShift,
            frames,
            PitchFftFrameSize,
            PitchOversampling,
            OutputSampleRate,
            _pitchRightBuffer);

        for (var i = 0; i < frames; i++)
        {
            var offset = i * BytesPerSampleFrame;
            var left = (short)Math.Clamp(_pitchLeftBuffer[i] * 32767f, short.MinValue, short.MaxValue);
            var right = (short)Math.Clamp(_pitchRightBuffer[i] * 32767f, short.MinValue, short.MaxValue);

            pcmBuffer[offset] = (byte)(left & 0xff);
            pcmBuffer[offset + 1] = (byte)((left >> 8) & 0xff);
            pcmBuffer[offset + 2] = (byte)(right & 0xff);
            pcmBuffer[offset + 3] = (byte)((right >> 8) & 0xff);
        }
    }
}