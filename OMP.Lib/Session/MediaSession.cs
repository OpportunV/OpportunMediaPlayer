using System.Threading.Channels;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Extensions;
using OMP.Lib.Interop;
using OMP.Lib.Threading;
using OMP.Lib.Video;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OMP.Lib.Session;

internal sealed unsafe class MediaSession : IMediaSession
{
    public event Action<VideoFrame>? VideoFrameReady;

    public IReadOnlyList<AudioOutput> AudioOutputs { get; }

    public IReadOnlyList<AudioRoute> AudioRoutes => _audioRoutes.AsReadOnly();

    public IReadOnlyDictionary<int, OutputVolumeState> OutputVolumes => _outputVolumes.AsReadOnly();

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

    public bool IsMuted { get; private set; }

    public double MasterVolume { get; private set; } = 1.0;

    public double Speed => _clock.Speed;

    public double VideoFps { get; private set; }

    public double VideoDecodeFps { get; private set; }

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    private string? _lastLoopErrorMessage;
    private readonly Stopwatch _loopErrorStopwatch = Stopwatch.StartNew();
    private int _suppressedLoopErrors;

    private readonly Channel<PacketRef> _audioChannel;
    private readonly List<AudioPipeline> _audioPipelines = [];
    private readonly List<AudioRoute> _audioRoutes = [];

    private readonly Dictionary<int, OutputVolumeState> _outputVolumes = [];
    private readonly int _audioBufferDurationSeconds;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly PlaybackClock _clock = new();

    private readonly PipelineWorker _demuxWorker;
    private readonly PipelineWorker _audioWorker;
    private readonly PipelineWorker _videoWorker;
    private readonly PipelineWorker _videoRenderWorker;
    private readonly Lock _formatSync = new();
    private readonly Lock _seekSync = new();

    private readonly AVFormatContext* _formatContext;

    private readonly Channel<PacketRef> _videoChannel;

    private readonly VideoPipeline? _videoPipeline;

    private readonly double _fpsSampleWindowMs;
    private int _videoFramesRendered;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private bool _isPlaying;
    private bool _awaitingFirstFrame = true;
    private double? _pendingSeekTargetSeconds;
    private int _seekGeneration;

    private const int NoVideoIdleSleepMs = 2;
    private const int FrameNotReadySleepMs = 1;
    private const int RenderErrorBackoffSleepMs = 5;
    private const double MaxFrameLagSeconds = 0.2;
    private const double EarlyFrameWaitThresholdSeconds = 0.03;
    private const double SeekFrameSkipEpsilonSeconds = 0.01;
    private const double SeekLookbackSeconds = 1;
    private const double LoopErrorLogIntervalMs = 5000;

    public MediaSession(string filePath, PlaybackTuningOptions options, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MediaSession>();

        _audioChannel = Channel.CreateBounded<PacketRef>(
            new BoundedChannelOptions(options.AudioChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        _videoChannel = Channel.CreateBounded<PacketRef>(
            new BoundedChannelOptions(options.VideoChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        _audioBufferDurationSeconds = options.BufferDurationSeconds;
        _fpsSampleWindowMs = options.FpsSampleWindowMs;

        int openResult;
        fixed (AVFormatContext** fc = &_formatContext)
        {
            openResult = ffmpeg.avformat_open_input(fc, filePath, null, null);
        }

        if (openResult != 0)
        {
            _logger.LogError("Could not open {FilePath}: {Error}.", filePath, FFmpegError.Describe(openResult));
            throw new ApplicationException("Could not open file.");
        }

        var streamInfoResult = ffmpeg.avformat_find_stream_info(_formatContext, null);
        if (streamInfoResult < 0)
        {
            _logger.LogError(
                "Could not read stream info for {FilePath}: {Error}.",
                filePath,
                FFmpegError.Describe(streamInfoResult));
            throw new ApplicationException("Could not find stream info.");
        }

        FileName = Path.GetFileNameWithoutExtension(filePath);

        for (var i = 0; i < _formatContext->nb_streams; i++)
        {
            var stream = _formatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                _videoPipeline = new VideoPipeline(_formatContext, i, _cancellationTokenSource.Token, loggerFactory);
                break;
            }
        }

        AudioStreams = new AudioScanner(loggerFactory).GetAudioStreams(_formatContext);
        AudioOutputs = new OutputScanner(loggerFactory).ScanOutputs();

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

        _logger.LogInformation(
            "Opened {FileName}: duration {Duration:c}, {AudioStreamCount} audio stream(s), " +
            "{OutputCount} output(s), video={HasVideo}.",
            FileName,
            Duration,
            AudioStreams.Count,
            AudioOutputs.Count,
            _videoPipeline is not null);

        if (AudioStreams.Count > 0 && AudioOutputs.Count > 0)
        {
            SetAudioRoutes([new AudioRoute(AudioStreams[0], AudioOutputs[0])]);
        }
        else
        {
            _logger.LogWarning(
                "No default audio route: {AudioStreamCount} audio stream(s), {OutputCount} output(s).",
                AudioStreams.Count,
                AudioOutputs.Count);
        }
    }

    public void SetAudioRoutes(IEnumerable<AudioRoute> routes)
    {
        var wasPlaying = _isPlaying;
        Pause();
        _audioPipelines.ForEach(p => p.Flush());

        ClearAudioPipelines();
        _audioRoutes.AddRange(routes);

        foreach (var route in _audioRoutes)
        {
            lock (_formatSync)
            {
                _audioPipelines.Add(
                    new AudioPipeline(
                        _formatContext,
                        route.Stream.Id,
                        route.Output,
                        _cancellationTokenSource.Token,
                        _audioBufferDurationSeconds,
                        _loggerFactory));
            }

            _logger.LogDebug(
                "Audio route: '{Title}' [{Language}] -> '{FriendlyName}'.",
                route.Stream.Title,
                route.Stream.Language,
                route.Output.FriendlyName);
        }

        _logger.LogInformation("Set {RouteCount} audio route(s).", _audioRoutes.Count);

        _audioPipelines.ForEach(p => p.SetSpeed(Speed));
        ApplyVolumeToPipelines();

        if (wasPlaying)
        {
            Play();
        }
    }

    public void SetMasterVolume(double volume)
    {
        MasterVolume = Math.Clamp(volume, AudioVolumeLimits.Min, AudioVolumeLimits.Max);
        ApplyVolumeToPipelines();
    }

    public void SetMasterMuted(bool muted)
    {
        IsMuted = muted;
        _logger.LogInformation("Master mute {State}.", muted ? "on" : "off");
        ApplyVolumeToPipelines();
    }

    public void SetOutputVolume(int outputId, double volume)
    {
        _outputVolumes[outputId] = GetOutputVolumeState(outputId)
            with { Volume = Math.Clamp(volume, AudioVolumeLimits.Min, AudioVolumeLimits.Max) };
        ApplyVolumeToPipelines();
    }

    public void SetOutputMuted(int outputId, bool muted)
    {
        _outputVolumes[outputId] = GetOutputVolumeState(outputId) with { Muted = muted };
        ApplyVolumeToPipelines();
    }

    public void Play()
    {
        _isPlaying = true;

        if (!_clock.IsRunning)
        {
            _awaitingFirstFrame = true;

            if (_videoPipeline is null)
            {
                _clock.Start();
                _awaitingFirstFrame = false;
            }
        }

        _demuxWorker.Resume();
        _audioWorker.Resume();
        _videoWorker.Resume();
        _videoRenderWorker.Resume();

        _audioPipelines.ForEach(p => p.Play());
    }

    public void Pause()
    {
        _isPlaying = false;
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

            Interlocked.Increment(ref _seekGeneration);

            var targetSeconds = Math.Clamp(target.TotalSeconds, 0, Duration.TotalSeconds);
            var wasPlaying = _isPlaying;
            Pause();
            DrainPacketChannel(_audioChannel);
            DrainPacketChannel(_videoChannel);
            var seekTargetSeconds = _videoPipeline is null
                ? targetSeconds
                : Math.Max(0, targetSeconds - SeekLookbackSeconds);

            bool seeked;
            int seekResult;
            lock (_formatSync)
            {
                seeked = TrySeekToVideoTarget(seekTargetSeconds, out seekResult);
            }

            if (!seeked)
            {
                _logger.LogWarning(
                    "Seek to {TargetSeconds:F3}s failed: {Error}.",
                    targetSeconds,
                    FFmpegError.Describe(seekResult));
            }
            else
            {
                _clock.Rebase(targetSeconds);
                _awaitingFirstFrame = true;
                _pendingSeekTargetSeconds = targetSeconds;

                _audioPipelines.ForEach(pipeline =>
                {
                    pipeline.Flush();
                    pipeline.ResetClock(targetSeconds);
                });
                _videoPipeline?.Flush();
            }

            if (wasPlaying)
            {
                Play();
            }
        }
    }

    public void SetSpeed(double speed)
    {
        _clock.SetSpeed(Math.Clamp(speed, PlaybackSpeedLimits.Min, PlaybackSpeedLimits.Max));
        _audioPipelines.ForEach(p => p.SetSpeed(Speed));
        Seek(CurrentTime);

        _logger.LogInformation("Playback speed set to {Speed:F2}x.", Speed);
    }

    public void Dispose()
    {
        _logger.LogDebug("Disposing session for {FileName}.", FileName);

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
            var generation = Volatile.Read(ref _seekGeneration);

            if (_audioPipelines.Any(pipeline => pipeline.StreamIndex == streamIndex))
            {
                var cloned = ffmpeg.av_packet_alloc();
                ffmpeg.av_packet_ref(cloned, packet);
                var packetRef = new PacketRef(cloned, generation);
                if (!_audioChannel.Writer.TryWrite(packetRef))
                {
                    ffmpeg.av_packet_free(&cloned);
                }
            }

            if (streamIndex == _videoPipeline?.StreamIndex)
            {
                var cloned = ffmpeg.av_packet_alloc();
                ffmpeg.av_packet_ref(cloned, packet);
                var packetRef = new PacketRef(cloned, generation);
                if (!_videoChannel.Writer.TryWriteBlocking(packetRef, _cancellationTokenSource.Token))
                {
                    ffmpeg.av_packet_free(&cloned);
                }
            }

            ffmpeg.av_packet_unref(packet);
        }

        ffmpeg.av_packet_free(&packet);
        _logger.LogDebug("{Role} worker stopping.", worker.Role);
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

            if (packetRef.Generation == Volatile.Read(ref _seekGeneration))
            {
                foreach (var pipeline in _audioPipelines)
                {
                    if (pipeline.StreamIndex == packet->stream_index)
                    {
                        pipeline.Enqueue(packet);
                    }
                }
            }

            ffmpeg.av_packet_free(&packet);
        }

        _logger.LogDebug("{Role} worker stopping.", worker.Role);
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

            if (packetRef.Generation == Volatile.Read(ref _seekGeneration))
            {
                _videoPipeline?.Enqueue(packet);
            }

            ffmpeg.av_packet_free(&packet);
        }

        _logger.LogDebug("{Role} worker stopping.", worker.Role);
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
                    PumpAudioOnly();
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
                    bool skipFrame;
                    lock (_seekSync)
                    {
                        if (_pendingSeekTargetSeconds.HasValue)
                        {
                            var pendingSeekTargetSeconds = _pendingSeekTargetSeconds.Value;

                            if (frame.TimeSeconds + SeekFrameSkipEpsilonSeconds < pendingSeekTargetSeconds)
                            {
                                skipFrame = true;
                            }
                            else
                            {
                                _clock.Rebase(pendingSeekTargetSeconds);
                                _pendingSeekTargetSeconds = null;
                                _clock.Start();
                                _awaitingFirstFrame = false;
                                skipFrame = false;
                            }
                        }
                        else
                        {
                            _clock.Rebase(frame.TimeSeconds);
                            _clock.Start();
                            _awaitingFirstFrame = false;
                            skipFrame = false;
                        }
                    }

                    if (skipFrame)
                    {
                        _videoPipeline.Pop();
                        continue;
                    }

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

                if (_fpsStopwatch.ElapsedMilliseconds >= _fpsSampleWindowMs)
                {
                    VideoFps = _videoFramesRendered * _fpsSampleWindowMs / _fpsStopwatch.ElapsedMilliseconds;
                    VideoDecodeFps = _videoPipeline.DecodeFps;
                    _videoFramesRendered = 0;
                    _fpsStopwatch.Restart();
                }
            }
            catch (Exception ex)
            {
                LogLoopError(ex);
                Thread.Sleep(RenderErrorBackoffSleepMs);
            }
        }

        _logger.LogDebug("{Role} worker stopping.", worker.Role);
    }

    private void PumpAudioOnly()
    {
        if (_audioPipelines.Count == 0)
        {
            return;
        }

        lock (_seekSync)
        {
            if (_pendingSeekTargetSeconds.HasValue)
            {
                _audioPipelines.ForEach(p => p.DiscardBefore(_pendingSeekTargetSeconds.Value));
                _pendingSeekTargetSeconds = null;
            }

            _awaitingFirstFrame = false;
        }

        var playbackTime = _clock.CurrentSeconds;
        _audioPipelines.ForEach(p => p.PumpToOutput(playbackTime));
    }

    private void LogLoopError(Exception ex)
    {
        if (ex.Message != _lastLoopErrorMessage)
        {
            _lastLoopErrorMessage = ex.Message;
            _suppressedLoopErrors = 0;
            _loopErrorStopwatch.Restart();
            _logger.LogError(ex, "Presentation loop iteration failed.");
            return;
        }

        _suppressedLoopErrors++;

        if (_loopErrorStopwatch.ElapsedMilliseconds < LoopErrorLogIntervalMs)
        {
            return;
        }

        _logger.LogError(
            ex,
            "Presentation loop iteration failed ({SuppressedCount} identical failure(s) suppressed).",
            _suppressedLoopErrors);
        _suppressedLoopErrors = 0;
        _loopErrorStopwatch.Restart();
    }

    private void ApplyVolumeToPipelines()
    {
        for (var i = 0; i < _audioPipelines.Count && i < _audioRoutes.Count; i++)
        {
            _audioPipelines[i].SetAmplitude(GetEffectiveAmplitude(_audioRoutes[i].Output.Id));
        }
    }

    private float GetEffectiveAmplitude(int outputId)
    {
        var state = GetOutputVolumeState(outputId);

        if (IsMuted || state.Muted)
        {
            return 0f;
        }

        return (float)(AudioGainProcessor.ToAmplitude(MasterVolume) * AudioGainProcessor.ToAmplitude(state.Volume));
    }

    private OutputVolumeState GetOutputVolumeState(int outputId)
    {
        return _outputVolumes.TryGetValue(outputId, out var state)
            ? state
            : new OutputVolumeState(1.0, false);
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

    private bool TrySeekToVideoTarget(double targetSeconds, out int result)
    {
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