using System.Threading.Channels;
using FFmpeg.AutoGen;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Extensions;
using OMP.Lib.Video;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OMP.Lib.Session;

public sealed unsafe class MediaSession : IMediaSession
{
    public event Action<VideoFrame>? VideoFrameReady;

    public IReadOnlyList<AudioOutput> AudioOutputs { get; }

    public IReadOnlyList<(AudioStream audioStream, AudioOutput audioOutput)> AudioRoutes => _audioRoutes.AsReadOnly();

    public IReadOnlyList<AudioStream> AudioStreams { get; }

    public TimeSpan CurrentTime => TimeSpan.FromSeconds(GetPlaybackTimeSeconds());

    public TimeSpan Duration
    {
        get
        {
            lock (_formatSync)
            {
                return _formatContext->duration > 0
                    ? TimeSpan.FromSeconds(_formatContext->duration / (double)ffmpeg.AV_TIME_BASE)
                    : TimeSpan.Zero;
            }
        }
    }

    public string FileName { get; }
    public double Speed { get; private set; } = 1.0;
    public double VideoFps { get; private set; }
    public double VideoDecodeFps { get; private set; }

    private readonly Channel<PacketRef> _audioChannel = Channel.CreateBounded<PacketRef>(
        new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly List<AudioPipeline> _audioPipelines = [];
    private readonly List<(AudioStream audioStream, AudioOutput audioOutput)> _audioRoutes = [];
    private readonly Thread _audioThread;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly Thread _demuxThread;
    private readonly Lock _formatSync = new();
    private readonly Lock _seekSync = new();

    private readonly AVFormatContext* _formatContext;

    private readonly Channel<PacketRef> _videoChannel = Channel.CreateBounded<PacketRef>(
        new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly VideoPipeline? _videoPipeline;
    private readonly Thread _videoThread;
    private readonly Thread _videoRenderThread;

    private volatile bool _paused = true;
    private volatile bool _running = true;
    private int _videoFramesRendered;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private double _playbackClockBaseSeconds;
    private long _playbackClockStartTicks;
    private bool _playbackClockRunning;
    private bool _awaitingFirstFrame = true;

    public MediaSession(string filePath)
    {
        fixed (AVFormatContext** fc = &_formatContext)
        {
            if (ffmpeg.avformat_open_input(fc, filePath, null, null) != 0)
            {
                throw new ApplicationException("Could not open file.");
            }
        }

        if (ffmpeg.avformat_find_stream_info(_formatContext, null) < 0)
        {
            throw new ApplicationException("Could not find stream info.");
        }

        FileName = Path.GetFileNameWithoutExtension(filePath);

        for (var i = 0; i < _formatContext->nb_streams; i++)
        {
            var stream = _formatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                _videoPipeline = new VideoPipeline(_formatContext, i);
                break;
            }
        }

        AudioStreams = new AudioScanner().GetAudioStreams(_formatContext);
        AudioOutputs = new OutputScanner().ScanOutputs();

        _demuxThread = new Thread(DemuxLoop) { IsBackground = true };
        _audioThread = new Thread(AudioLoop) { IsBackground = true };
        _videoThread = new Thread(VideoLoop) { IsBackground = true };
        _videoRenderThread = new Thread(VideoRenderLoop) { IsBackground = true };

        _demuxThread.Start();
        _audioThread.Start();
        _videoThread.Start();
        _videoRenderThread.Start();

        SetAudioRoutes([(AudioStreams[0], AudioOutputs[0])]);
    }

    public void SetAudioRoutes(IEnumerable<(AudioStream stream, AudioOutput output)> routes)
    {
        var wasPlaying = !_paused;
        Pause();
        _audioPipelines.ForEach(p => p.Flush());

        ClearAudioPipelines();
        _audioRoutes.AddRange(routes);

        foreach (var (stream, output) in _audioRoutes)
        {
            lock (_formatSync)
            {
                _audioPipelines.Add(
                    new AudioPipeline(_formatContext, stream.Id, output.Id, _cancellationTokenSource.Token));
            }
        }

        _audioPipelines.ForEach(p => p.SetSpeed(Speed));

        if (wasPlaying)
        {
            Play();
        }
    }

    public void Play()
    {
        if (!_playbackClockRunning)
        {
            _playbackClockStartTicks = Stopwatch.GetTimestamp();
            _playbackClockRunning = true;
            _awaitingFirstFrame = true;
        }

        _paused = false;
        _audioPipelines.ForEach(p => p.Play());
    }

    public void Pause()
    {
        _playbackClockBaseSeconds = GetPlaybackTimeSeconds();
        _playbackClockRunning = false;
        _paused = true;
        _audioPipelines.ForEach(p => p.Pause());
    }

    public void Step(TimeSpan offset)
    {
        var targetSeconds = CurrentTime + offset;
        Seek(targetSeconds);
    }

    public void Seek(TimeSpan target)
    {
        lock (_seekSync)
        {
            if (_audioPipelines.Count == 0 && _videoPipeline is null)
            {
                return;
            }

            var targetSeconds = Math.Clamp(target.TotalSeconds, 0, Duration.TotalSeconds);
            var wasPlaying = !_paused;
            Pause();
            DrainPacketChannel(_audioChannel);
            DrainPacketChannel(_videoChannel);
            var targetPts = (long)Math.Round(targetSeconds * ffmpeg.AV_TIME_BASE);

            lock (_formatSync)
            {
                if (ffmpeg.av_seek_frame(
                        _formatContext,
                        -1,
                        targetPts,
                        ffmpeg.AVSEEK_FLAG_ANY) < 0)
                {
                    Console.WriteLine("Error during seek.");
                    return;
                }

                ffmpeg.avformat_flush(_formatContext);
            }

            _playbackClockBaseSeconds = targetSeconds;
            _playbackClockRunning = false;
            _awaitingFirstFrame = true;

            _audioPipelines.ForEach(pipeline =>
            {
                pipeline.Flush();
                pipeline.ResetClock(targetSeconds);
            });
            _videoPipeline?.Flush();

            if (wasPlaying)
            {
                Play();
            }
        }
    }

    public void SetSpeed(double speed)
    {
        _playbackClockBaseSeconds = GetPlaybackTimeSeconds();
        Speed = Math.Clamp(speed, 0.5, 2.0);
        if (_playbackClockRunning)
        {
            _playbackClockStartTicks = Stopwatch.GetTimestamp();
        }

        // TODO: Perhaps need to flush same way as in the seek method.
        _audioPipelines.ForEach(p => p.SetSpeed(Speed));
    }

    public void Dispose()
    {
        _running = false;
        _paused = false;
        _cancellationTokenSource.Cancel(false);
        _cancellationTokenSource.Dispose();
        _demuxThread.Join();
        _audioThread.Join();
        _videoThread.Join();
        _videoRenderThread.Join();

        VideoFrameReady = null;
        ClearAudioPipelines();
        _videoPipeline?.Dispose();

        fixed (AVFormatContext** fc = &_formatContext)
        {
            ffmpeg.avformat_close_input(fc);
        }
    }

    private void DemuxLoop()
    {
        var packet = ffmpeg.av_packet_alloc();

        while (_running)
        {
            if (_paused)
            {
                Thread.Sleep(5);
                continue;
            }

            int readResult;
            lock (_formatSync)
            {
                readResult = ffmpeg.av_read_frame(_formatContext, packet);
            }

            if (readResult < 0)
            {
                break;
            }

            var streamIndex = packet->stream_index;

            if (_audioPipelines.Any(pipeline => pipeline.StreamIndex == streamIndex))
            {
                var cloned = ffmpeg.av_packet_alloc();
                ffmpeg.av_packet_ref(cloned, packet);
                var packetRef = new PacketRef { Packet = cloned };
                if (!_audioChannel.Writer.TryWrite(packetRef))
                {
                    ffmpeg.av_packet_free(&cloned);
                }
            }

            if (streamIndex == _videoPipeline?.StreamIndex)
            {
                var cloned = ffmpeg.av_packet_alloc();
                ffmpeg.av_packet_ref(cloned, packet);
                var packetRef = new PacketRef { Packet = cloned };
                _videoChannel.Writer.Write(packetRef, _cancellationTokenSource.Token);
            }

            ffmpeg.av_packet_unref(packet);
        }

        ffmpeg.av_packet_free(&packet);
    }

    private void AudioLoop()
    {
        while (_running)
        {
            if (_paused)
            {
                Thread.Sleep(5);
                continue;
            }

            var packetRef = _audioChannel.Reader.Read(_cancellationTokenSource.Token);
            var packet = packetRef.Packet;

            foreach (var pipeline in _audioPipelines)
            {
                if (pipeline.StreamIndex == packet->stream_index)
                {
                    pipeline.Enqueue(packet);
                }
            }

            ffmpeg.av_packet_free(&packet);
        }
    }

    private void VideoLoop()
    {
        while (_running)
        {
            if (_paused)
            {
                Thread.Sleep(5);
                continue;
            }

            var packetRef = _videoChannel.Reader.Read(_cancellationTokenSource.Token);
            var packet = packetRef.Packet;
            _videoPipeline?.Enqueue(packet);
            ffmpeg.av_packet_free(&packet);
        }
    }

    private void VideoRenderLoop()
    {
        while (_running)
        {
            try
            {
                if (_paused || _videoPipeline is null)
                {
                    Thread.Sleep(2);
                    continue;
                }

                var playbackTime = GetPlaybackTimeSeconds();
                if (_audioPipelines.Count > 0)
                {
                    _audioPipelines.ForEach(p => p.PumpToOutput(playbackTime));
                }

                if (!_videoPipeline.TryPeek(out var frame))
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (_awaitingFirstFrame)
                {
                    _playbackClockBaseSeconds = frame.TimeSeconds;
                    _playbackClockStartTicks = Stopwatch.GetTimestamp();
                    _playbackClockRunning = true;
                    _awaitingFirstFrame = false;
                    playbackTime = frame.TimeSeconds;
                }

                var leadSeconds = frame.TimeSeconds - playbackTime;

                if (leadSeconds < -0.2)
                {
                    _videoPipeline.Pop();
                    continue;
                }

                if (leadSeconds > 0.03)
                {
                    Thread.Sleep(1);
                    continue;
                }

                VideoFrameReady?.Invoke(frame);
                _videoPipeline.Pop();
                _videoFramesRendered++;

                if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    VideoFps = _videoFramesRendered * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
                    VideoDecodeFps = _videoPipeline.DecodeFps;
                    _videoFramesRendered = 0;
                    _fpsStopwatch.Restart();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoRenderLoop error: {ex.Message}");
                Thread.Sleep(5);
            }
        }
    }

    private void ClearAudioPipelines()
    {
        _audioPipelines.ForEach(p => p.Dispose());
        _audioPipelines.Clear();
        _audioRoutes.Clear();
    }

    private static void DrainPacketChannel(Channel<PacketRef> channel)
    {
        while (channel.Reader.TryRead(out var packetRef))
        {
            var packet = packetRef.Packet;
            ffmpeg.av_packet_free(&packet);
        }
    }

    private double GetPlaybackTimeSeconds()
    {
        if (!_playbackClockRunning)
        {
            return _playbackClockBaseSeconds;
        }

        var elapsed = Stopwatch.GetElapsedTime(_playbackClockStartTicks).TotalSeconds;
        return _playbackClockBaseSeconds + elapsed * Speed;
    }
}