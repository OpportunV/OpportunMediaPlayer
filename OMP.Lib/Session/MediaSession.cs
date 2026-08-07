using System.Threading.Channels;
using FFmpeg.AutoGen;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Extensions;
using OMP.Lib.Threading;
using OMP.Lib.Video;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OMP.Lib.Session;

internal sealed unsafe class MediaSession : IMediaSession
{
    public event Action<VideoFrame>? VideoFrameReady;

    public IReadOnlyList<AudioOutput> AudioOutputs { get; }

    public IReadOnlyList<(AudioStream audioStream, AudioOutput audioOutput)> AudioRoutes => _audioRoutes.AsReadOnly();

    public IReadOnlyList<AudioStream> AudioStreams { get; }

    public TimeSpan CurrentTime => TimeSpan.FromSeconds(_clock.CurrentSeconds);

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
    public double Speed => _clock.Speed;
    public double VideoFps { get; private set; }
    public double VideoDecodeFps { get; private set; }

    private readonly Channel<PacketRef> _audioChannel = Channel.CreateBounded<PacketRef>(
        new BoundedChannelOptions(AudioChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly List<AudioPipeline> _audioPipelines = [];
    private readonly List<(AudioStream audioStream, AudioOutput audioOutput)> _audioRoutes = [];
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly PlaybackClock _clock = new();

    private readonly PipelineWorker _demuxWorker;
    private readonly PipelineWorker _audioWorker;
    private readonly PipelineWorker _videoWorker;
    private readonly PipelineWorker _videoRenderWorker;
    private readonly Lock _formatSync = new();
    private readonly Lock _seekSync = new();

    private readonly AVFormatContext* _formatContext;

    private readonly Channel<PacketRef> _videoChannel = Channel.CreateBounded<PacketRef>(
        new BoundedChannelOptions(VideoChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly VideoPipeline? _videoPipeline;

    private int _videoFramesRendered;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private bool _awaitingFirstFrame = true;
    private double? _pendingSeekTargetSeconds;

    private const int AudioChannelCapacity = 200;
    private const int VideoChannelCapacity = 10;
    private const int NoVideoIdleSleepMs = 2;
    private const int FrameNotReadySleepMs = 1;
    private const int RenderErrorBackoffSleepMs = 5;
    private const double MaxFrameLagSeconds = 0.2;
    private const double EarlyFrameWaitThresholdSeconds = 0.03;
    private const double SeekFrameSkipEpsilonSeconds = 0.01;
    private const double SeekLookbackSeconds = 1;
    private const double FpsSampleWindowMs = 1000;

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
                _videoPipeline = new VideoPipeline(_formatContext, i, _cancellationTokenSource.Token);
                break;
            }
        }

        AudioStreams = new AudioScanner().GetAudioStreams(_formatContext);
        AudioOutputs = new OutputScanner().ScanOutputs();

        _demuxWorker = new PipelineWorker(PipelineWorkerRole.Demux, _cancellationTokenSource.Token);
        _audioWorker = new PipelineWorker(PipelineWorkerRole.Audio, _cancellationTokenSource.Token);
        _videoWorker = new PipelineWorker(PipelineWorkerRole.Video, _cancellationTokenSource.Token);
        _videoRenderWorker = new PipelineWorker(PipelineWorkerRole.VideoRender, _cancellationTokenSource.Token);

        _demuxWorker.Pause();
        _audioWorker.Pause();
        _videoWorker.Pause();
        _videoRenderWorker.Pause();

        _demuxWorker.Start(DemuxLoop);
        _audioWorker.Start(AudioLoop);
        _videoWorker.Start(VideoLoop);
        _videoRenderWorker.Start(VideoRenderLoop);

        SetAudioRoutes([(AudioStreams[0], AudioOutputs[0])]);
    }

    public void SetAudioRoutes(IEnumerable<(AudioStream stream, AudioOutput output)> routes)
    {
        var wasPlaying = _clock.IsRunning;
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
        if (!_clock.IsRunning)
        {
            _clock.Start();
            _awaitingFirstFrame = true;
        }

        _demuxWorker.Resume();
        _audioWorker.Resume();
        _videoWorker.Resume();
        _videoRenderWorker.Resume();

        _audioPipelines.ForEach(p => p.Play());
    }

    public void Pause()
    {
        _clock.Stop();

        _demuxWorker.Pause();
        _audioWorker.Pause();
        _videoWorker.Pause();
        _videoRenderWorker.Pause();

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
            var wasPlaying = _clock.IsRunning;
            Pause();
            DrainPacketChannel(_audioChannel);
            DrainPacketChannel(_videoChannel);
            var seekTargetSeconds = _videoPipeline is null
                ? targetSeconds
                : Math.Max(0, targetSeconds - SeekLookbackSeconds);

            lock (_formatSync)
            {
                if (!TrySeekToVideoTarget(seekTargetSeconds))
                {
                    Console.WriteLine("Error during seek.");
                    return;
                }
            }

            _clock.Rebase(targetSeconds);
            _awaitingFirstFrame = true;
            _pendingSeekTargetSeconds = targetSeconds;

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
        _clock.SetSpeed(Math.Clamp(speed, PlaybackSpeedLimits.Min, PlaybackSpeedLimits.Max));

        // TODO: Perhaps need to flush same way as in the seek method.
        _audioPipelines.ForEach(p => p.SetSpeed(Speed));
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel(false);
        _cancellationTokenSource.Dispose();

        _demuxWorker.Join();
        _audioWorker.Join();
        _videoWorker.Join();
        _videoRenderWorker.Join();

        _demuxWorker.Dispose();
        _audioWorker.Dispose();
        _videoWorker.Dispose();
        _videoRenderWorker.Dispose();

        VideoFrameReady = null;
        ClearAudioPipelines();
        _videoPipeline?.Dispose();

        fixed (AVFormatContext** fc = &_formatContext)
        {
            ffmpeg.avformat_close_input(fc);
        }
    }

    private void DemuxLoop(PipelineWorker worker)
    {
        var packet = ffmpeg.av_packet_alloc();

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
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
                if (!_videoChannel.Writer.TryWriteBlocking(packetRef, _cancellationTokenSource.Token))
                {
                    ffmpeg.av_packet_free(&cloned);
                }
            }

            ffmpeg.av_packet_unref(packet);
        }

        ffmpeg.av_packet_free(&packet);
    }

    private void AudioLoop(PipelineWorker worker)
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
            }

            if (!_audioChannel.Reader.TryReadBlocking(out var packetRef, _cancellationTokenSource.Token))
            {
                break;
            }

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

    private void VideoLoop(PipelineWorker worker)
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
            }

            if (!_videoChannel.Reader.TryReadBlocking(out var packetRef, _cancellationTokenSource.Token))
            {
                break;
            }

            var packet = packetRef.Packet;

            _videoPipeline?.Enqueue(packet);
            ffmpeg.av_packet_free(&packet);
        }
    }

    private void VideoRenderLoop(PipelineWorker worker)
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
            }

            try
            {
                if (_videoPipeline is null)
                {
                    Thread.Sleep(NoVideoIdleSleepMs);
                    continue;
                }

                if (_awaitingFirstFrame && _pendingSeekTargetSeconds.HasValue)
                {
                    _audioPipelines.ForEach(p => p.DiscardBefore(_pendingSeekTargetSeconds.Value));
                }

                var playbackTime = _clock.CurrentSeconds;
                if (_audioPipelines.Count > 0 && !_awaitingFirstFrame)
                {
                    _audioPipelines.ForEach(p => p.PumpToOutput(playbackTime));
                }

                if (!_videoPipeline.TryPeek(out var frame))
                {
                    Thread.Sleep(FrameNotReadySleepMs);
                    continue;
                }

                if (_awaitingFirstFrame)
                {
                    if (_pendingSeekTargetSeconds.HasValue)
                    {
                        var pendingSeekTargetSeconds = _pendingSeekTargetSeconds.Value;

                        if (frame.TimeSeconds + SeekFrameSkipEpsilonSeconds < pendingSeekTargetSeconds)
                        {
                            _videoPipeline.Pop();
                            continue;
                        }

                        _clock.Rebase(pendingSeekTargetSeconds);
                        _pendingSeekTargetSeconds = null;
                    }
                    else
                    {
                        _clock.Rebase(frame.TimeSeconds);
                    }

                    _clock.Start();
                    _awaitingFirstFrame = false;
                    playbackTime = _clock.CurrentSeconds;
                }

                var leadSeconds = frame.TimeSeconds - playbackTime;

                if (leadSeconds < -MaxFrameLagSeconds)
                {
                    _videoPipeline.Pop();
                    continue;
                }

                if (leadSeconds > EarlyFrameWaitThresholdSeconds)
                {
                    Thread.Sleep(FrameNotReadySleepMs);
                    continue;
                }

                VideoFrameReady?.Invoke(frame);
                _videoPipeline.Pop();
                _videoFramesRendered++;

                if (_fpsStopwatch.ElapsedMilliseconds >= FpsSampleWindowMs)
                {
                    VideoFps = _videoFramesRendered * FpsSampleWindowMs / _fpsStopwatch.ElapsedMilliseconds;
                    VideoDecodeFps = _videoPipeline.DecodeFps;
                    _videoFramesRendered = 0;
                    _fpsStopwatch.Restart();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VideoRenderLoop error: {ex.Message}");
                Thread.Sleep(RenderErrorBackoffSleepMs);
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

    private bool TrySeekToVideoTarget(double targetSeconds)
    {
        int result;
        if (_videoPipeline is null)
        {
            var targetPts = (long)Math.Round(targetSeconds * ffmpeg.AV_TIME_BASE);
            result = ffmpeg.av_seek_frame(
                _formatContext,
                -1,
                targetPts,
                ffmpeg.AVSEEK_FLAG_BACKWARD);

            if (result >= 0)
            {
                ffmpeg.avformat_flush(_formatContext);
                return true;
            }

            return false;
        }

        var stream = _formatContext->streams[_videoPipeline.StreamIndex];
        var streamTimeBase = stream->time_base;
        var targetPtsInStreamTimeBase = (long)Math.Round(targetSeconds / ffmpeg.av_q2d(streamTimeBase));
        result = ffmpeg.av_seek_frame(
            _formatContext,
            _videoPipeline.StreamIndex,
            targetPtsInStreamTimeBase,
            ffmpeg.AVSEEK_FLAG_BACKWARD);

        if (result >= 0)
        {
            ffmpeg.avformat_flush(_formatContext);
            return true;
        }

        return false;
    }
}
