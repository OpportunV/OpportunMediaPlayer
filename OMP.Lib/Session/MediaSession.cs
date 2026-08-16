using System.Globalization;
using System.Threading.Channels;
using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Extensions;
using OMP.Lib.Interop;
using OMP.Lib.Subtitle;
using OMP.Lib.Threading;
using OMP.Lib.Video;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace OMP.Lib.Session;

internal sealed unsafe class MediaSession : IMediaSession
{
    public event Action<VideoFrame>? VideoFrameReady;

    public event Action? PlaybackEnded;

    public IReadOnlyList<AudioOutput> AudioOutputs { get; }

    public string? AudioOutputUnavailableReason { get; }

    public IReadOnlyList<AudioRoute> AudioRoutes => _audioRoutes.AsReadOnly();

    public IReadOnlyDictionary<int, OutputVolumeState> OutputVolumes => _outputVolumes.AsReadOnly();

    public IReadOnlyDictionary<int, double> OutputDelays => _outputDelays.AsReadOnly();

    public IReadOnlyList<AudioStream> AudioStreams { get; }

    public IReadOnlyList<SubtitleStream> SubtitleStreams { get; }

    public IReadOnlyList<SubtitleRoute> SubtitleRoutes => _subtitleRoutes.AsReadOnly();

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

    public string FilePath { get; }

    public bool IsMuted { get; private set; }

    public double MasterVolume { get; private set; } = 1.0;

    public double Speed => _clock.Speed;

    public bool HasVideo => _videoPipeline is not null;

    public double VideoFps { get; private set; }

    public double VideoDecodeFps { get; private set; }

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    private string? _lastLoopErrorMessage;
    private readonly Stopwatch _loopErrorStopwatch = Stopwatch.StartNew();
    private int _suppressedLoopErrors;

    private readonly List<AudioPipeline> _audioPipelines = [];
    private readonly List<AudioRoute> _audioRoutes = [];

    private readonly Channel<PacketRef> _subtitleChannel;
    private readonly List<SubtitlePipeline> _subtitlePipelines = [];
    private readonly List<SubtitleRoute> _subtitleRoutes = [];

    private readonly Dictionary<int, OutputVolumeState> _outputVolumes = [];
    private readonly Dictionary<int, double> _outputDelays = [];
    private readonly int _audioBufferDurationSeconds;
    private readonly int _audioPacketChannelCapacity;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly PlaybackClock _clock = new();

    private readonly PipelineWorker _demuxWorker;
    private readonly PipelineWorker? _videoWorker;
    private readonly PipelineWorker? _videoRenderWorker;
    private readonly PipelineWorker _subtitleWorker;
    private readonly PipelineWorker _sessionWorker;
    private readonly Lock _formatSync = new();
    private readonly Lock _seekSync = new();

    private readonly AVFormatContext* _formatContext;

    private readonly Channel<PacketRef> _videoChannel;

    private readonly VideoPipeline? _videoPipeline;

    private readonly double _fpsSampleWindowMs;
    private int _videoFramesRendered;
    private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();
    private readonly Stopwatch _syncLogStopwatch = Stopwatch.StartNew();
    private bool _isPlaying;
    private double? _pendingSeekTargetSeconds;
    private double _lastSeekTargetSeconds;
    private int _seekGeneration;
    private double _pendingDemuxPtsAnchorSeconds;
    private int _diagVideoGenerationMismatchCount;
    private readonly Dictionary<int, double> _demuxPtsBaselineOffsets = [];
    private readonly EndOfStreamTracker _endOfStreamTracker = new();

    private const double MaxFrameLagSeconds = 0.05;
    private const double EarlyFrameWaitThresholdSeconds = 0.03;
    private const double SeekLookbackSeconds = 1;
    private const double LoopErrorLogIntervalMs = 5000;
    private const double SyncLogIntervalMs = 1000;
    private const double MaxDemuxLookaheadSeconds = 3;
    private const double ZeroSeekEpsilonSeconds = 0.05;

    public MediaSession(string filePath, PlaybackTuningOptions options, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MediaSession>();

        FFmpegEnvironment.EnsureInitialized(_logger);

        _videoChannel = Channel.CreateBounded<PacketRef>(
            new BoundedChannelOptions(options.VideoChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        _subtitleChannel = Channel.CreateBounded<PacketRef>(
            new BoundedChannelOptions(options.SubtitleChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        _audioBufferDurationSeconds = options.BufferDurationSeconds;
        _audioPacketChannelCapacity = options.AudioChannelCapacity;
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

        FileName = Path.GetFileName(filePath);
        FilePath = filePath;

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
        var outputScanner = new OutputScanner(loggerFactory);
        AudioOutputs = outputScanner.ScanOutputs();
        AudioOutputUnavailableReason = outputScanner.UnavailableReason;
        SubtitleStreams = new SubtitleScanner(loggerFactory).GetSubtitleStreams(_formatContext);

        _demuxWorker = new PipelineWorker(PipelineWorkerRole.Demux, _cancellationTokenSource.Token);
        _subtitleWorker = new PipelineWorker(PipelineWorkerRole.Subtitle, _cancellationTokenSource.Token);
        _sessionWorker = new PipelineWorker(PipelineWorkerRole.Session, _cancellationTokenSource.Token);

        _demuxWorker.Pause();
        _subtitleWorker.Pause();
        _sessionWorker.Pause();

        _demuxWorker.Start(DemuxLoop);
        _subtitleWorker.Start(SubtitleLoop);
        _sessionWorker.Start(SessionLoop);

        if (_videoPipeline is not null)
        {
            _videoWorker = new PipelineWorker(PipelineWorkerRole.Video, _cancellationTokenSource.Token);
            _videoRenderWorker = new PipelineWorker(PipelineWorkerRole.VideoRender, _cancellationTokenSource.Token);

            _videoWorker.Pause();
            _videoRenderWorker.Pause();

            _videoWorker.Start(VideoLoop);
            _videoRenderWorker.Start(VideoRenderLoop);
        }

        _logger.LogInformation(
            "Opened {FilePath}: duration {Duration:c}, {AudioStreamCount} audio stream(s), " +
            "{SubtitleStreamCount} subtitle stream(s), {OutputCount} output(s), video={HasVideo}.",
            FilePath,
            Duration,
            AudioStreams.Count,
            SubtitleStreams.Count,
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

        foreach (var route in routes)
        {
            AudioPipeline pipeline;
            lock (_formatSync)
            {
                try
                {
                    pipeline = new AudioPipeline(
                        _formatContext,
                        route.Stream.Id,
                        route.Output,
                        _cancellationTokenSource.Token,
                        _audioBufferDurationSeconds,
                        _audioPacketChannelCapacity,
                        () => Volatile.Read(ref _seekGeneration),
                        () => _clock.CurrentSeconds,
                        _loggerFactory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Could not route '{Title}' [{Language}] -> '{FriendlyName}'; skipping this route.",
                        route.Stream.Title,
                        route.Stream.Language,
                        route.Output.FriendlyName);
                    continue;
                }
            }

            _audioRoutes.Add(route);
            _audioPipelines.Add(pipeline);

            _logger.LogDebug(
                "Audio route: '{Title}' [{Language}] -> '{FriendlyName}'.",
                route.Stream.Title,
                route.Stream.Language,
                route.Output.FriendlyName);
        }

        _logger.LogInformation("Set {RouteCount} audio route(s).", _audioRoutes.Count);

        _audioPipelines.ForEach(p => p.SetSpeed(Speed));
        ApplyVolumeToPipelines();
        ApplyDelayToPipelines();

        if (wasPlaying)
        {
            Play();
        }
    }

    public void SetSubtitleRoutes(IEnumerable<SubtitleRoute> routes)
    {
        var wasPlaying = _isPlaying;
        Pause();

        var newRoutes = routes.ToList();

        var pipelinesToKeep = _subtitlePipelines
            .Where(p => newRoutes.Any(r => r.Stream.Id == p.StreamIndex && r.ZoneId == p.ZoneId))
            .ToList();

        _subtitlePipelines.Except(pipelinesToKeep).ToList().ForEach(p => p.Dispose());
        _subtitlePipelines.Clear();
        _subtitlePipelines.AddRange(pipelinesToKeep);

        _subtitleRoutes.Clear();
        _subtitleRoutes.AddRange(newRoutes);

        foreach (var route in _subtitleRoutes)
        {
            if (_subtitlePipelines.Any(p => p.StreamIndex == route.Stream.Id && p.ZoneId == route.ZoneId))
            {
                continue;
            }

            lock (_formatSync)
            {
                _subtitlePipelines.Add(
                    new SubtitlePipeline(
                        _formatContext,
                        route.Stream.Id,
                        route.ZoneId,
                        _loggerFactory));
            }

            _logger.LogDebug(
                "Subtitle route: '{Title}' [{Language}] -> zone '{ZoneId}'.",
                route.Stream.Title,
                route.Stream.Language,
                route.ZoneId);
        }

        _logger.LogInformation("Set {RouteCount} subtitle route(s).", _subtitleRoutes.Count);

        if (wasPlaying)
        {
            Play();
        }
    }

    public IReadOnlyList<SubtitleCue> GetActiveSubtitleCues()
    {
        if (_subtitlePipelines.Count == 0)
        {
            return [];
        }

        var currentSeconds = _clock.CurrentSeconds;
        var cues = new List<SubtitleCue>();

        foreach (var pipeline in _subtitlePipelines)
        {
            cues.AddRange(pipeline.GetActiveCues(currentSeconds));
        }

        return cues;
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

    public void SetOutputDelay(int outputId, double delaySeconds)
    {
        _outputDelays[outputId] = Math.Clamp(delaySeconds, AudioDelayLimits.Min, AudioDelayLimits.Max);
        ApplyDelayToPipelines();
    }

    public void Play()
    {
        _isPlaying = true;

        _demuxWorker.Resume();
        _videoWorker?.Resume();
        _subtitleWorker.Resume();
        _sessionWorker.Resume();
        _audioPipelines.ForEach(p => p.Play());
        _clock.Start();
        _videoRenderWorker?.Resume();
    }

    public void Pause()
    {
        _isPlaying = false;
        _clock.Stop();

        _demuxWorker.Pause();
        _videoWorker?.Pause();
        _videoRenderWorker?.Pause();
        _subtitleWorker.Pause();
        _sessionWorker.Pause();

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
            DrainPacketChannel(_videoChannel);
            DrainPacketChannel(_subtitleChannel);
            var seekTargetSeconds = Math.Max(0, targetSeconds - SeekLookbackSeconds);

            bool seeked;
            int seekResult;
            lock (_formatSync)
            {
                seeked = TrySeekToTarget(seekTargetSeconds, out seekResult);
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
                _pendingSeekTargetSeconds = targetSeconds;
                _lastSeekTargetSeconds = targetSeconds;
                
                var ptsBaselineAnchorSeconds = _videoPipeline is null ? seekTargetSeconds : 0;
                _pendingDemuxPtsAnchorSeconds = ptsBaselineAnchorSeconds;
                _demuxPtsBaselineOffsets.Clear();
                _endOfStreamTracker.MarkStreamReadable();

                _audioPipelines.ForEach(pipeline =>
                {
                    pipeline.Flush();
                    pipeline.ResetClock(targetSeconds, ptsBaselineAnchorSeconds);
                });
                _videoPipeline?.Flush();
                _subtitlePipelines.ForEach(p => p.Flush());
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
        _videoWorker?.Join();
        _videoRenderWorker?.Join();
        _subtitleWorker.Join();
        _sessionWorker.Join();

        _demuxWorker.Dispose();
        _videoWorker?.Dispose();
        _videoRenderWorker?.Dispose();
        _subtitleWorker.Dispose();
        _sessionWorker.Dispose();

        VideoFrameReady = null;
        PlaybackEnded = null;
        ClearAudioPipelines();
        ClearSubtitlePipelines();
        _videoPipeline?.Dispose();

        fixed (AVFormatContext** fc = &_formatContext)
        {
            ffmpeg.avformat_close_input(fc);
        }
    }

    private void DemuxLoop(PipelineWorker worker)
    {
        var packet = ffmpeg.av_packet_alloc();

        var cancellationToken = _cancellationTokenSource.Token;

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
                _logger.LogWarning(
                    "Demux read failed at generation {Generation}: {Error} ({Code}).",
                    Volatile.Read(ref _seekGeneration),
                    FFmpegError.Describe(readResult),
                    readResult);
                _endOfStreamTracker.MarkEndOfStream();
                worker.Pause();
                continue;
            }

            _endOfStreamTracker.MarkStreamReadable();

            var streamIndex = packet->stream_index;

            var generation = Volatile.Read(ref _seekGeneration);

            if (packet->pts != ffmpeg.AV_NOPTS_VALUE)
            {
                var packetSeconds = packet->pts * ffmpeg.av_q2d(_formatContext->streams[streamIndex]->time_base);

                if (!_demuxPtsBaselineOffsets.TryGetValue(streamIndex, out var baselineOffset))
                {
                    baselineOffset = PtsBaselineDetector.DetectOffset(packetSeconds, _pendingDemuxPtsAnchorSeconds);
                    _demuxPtsBaselineOffsets[streamIndex] = baselineOffset;
                }

                ThrottleDemuxAhead(packetSeconds + baselineOffset, generation, worker);
            }

            foreach (var pipeline in _audioPipelines)
            {
                if (pipeline.StreamIndex == streamIndex)
                {
                    DispatchClonedPacket(packet, generation, pipeline.TryEnqueuePacket);
                }
            }

            if (streamIndex == _videoPipeline?.StreamIndex)
            {
                DispatchClonedPacket(
                    packet,
                    generation,
                    packetRef => _videoChannel.Writer.TryWriteBlocking(packetRef, cancellationToken));
            }

            if (_subtitlePipelines.Any(pipeline => pipeline.StreamIndex == streamIndex))
            {
                DispatchClonedPacket(packet, generation, _subtitleChannel.Writer.TryWrite);
            }

            ffmpeg.av_packet_unref(packet);
        }

        ffmpeg.av_packet_free(&packet);
        _logger.LogDebug("{Role} worker stopping.", worker.Role);
    }

    private void ThrottleDemuxAhead(double packetSeconds, int generation, PipelineWorker worker)
    {
        if (packetSeconds < _lastSeekTargetSeconds)
        {
            return;
        }

        while (packetSeconds - _clock.CurrentSeconds > MaxDemuxLookaheadSeconds &&
               generation == Volatile.Read(ref _seekGeneration) &&
               !_cancellationTokenSource.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
            }

            Thread.Sleep(5);
        }
    }

    private static void DispatchClonedPacket(AVPacket* packet, int generation, Func<PacketRef, bool> tryWrite)
    {
        var cloned = ffmpeg.av_packet_alloc();
        ffmpeg.av_packet_ref(cloned, packet);
        var packetRef = new PacketRef(cloned, generation);
        if (!tryWrite(packetRef))
        {
            ffmpeg.av_packet_free(&cloned);
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

            if (packetRef.Generation == Volatile.Read(ref _seekGeneration))
            {
                _videoPipeline?.Enqueue(packet);
            }
            else
            {
                Interlocked.Increment(ref _diagVideoGenerationMismatchCount);
            }

            ffmpeg.av_packet_free(&packet);
        }

        _logger.LogDebug("{Role} worker stopping.", worker.Role);
    }

    private void SubtitleLoop(PipelineWorker worker)
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
            }

            if (!_subtitleChannel.Reader.TryReadBlocking(out var packetRef, _cancellationTokenSource.Token))
            {
                break;
            }

            var packet = packetRef.Packet;

            if (packetRef.Generation == Volatile.Read(ref _seekGeneration))
            {
                foreach (var pipeline in _subtitlePipelines)
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

    private void VideoRenderLoop(PipelineWorker worker)
    {
        var videoPipeline = _videoPipeline!;

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
            }

            try
            {
                var playbackTime = _clock.CurrentSeconds;

                if (!videoPipeline.TryPeek(out var frame))
                {
                    Thread.Yield();
                    continue;
                }

                var leadSeconds = frame.TimeSeconds - playbackTime;

                if (leadSeconds < -MaxFrameLagSeconds)
                {
                    videoPipeline.Pop();
                    continue;
                }

                if (leadSeconds > EarlyFrameWaitThresholdSeconds)
                {
                    Thread.Yield();
                    continue;
                }

                VideoFrameReady?.Invoke(frame);
                videoPipeline.Pop();
                _videoFramesRendered++;

                if (_fpsStopwatch.ElapsedMilliseconds >= _fpsSampleWindowMs)
                {
                    VideoFps = _videoFramesRendered * _fpsSampleWindowMs / _fpsStopwatch.ElapsedMilliseconds;
                    VideoDecodeFps = videoPipeline.DecodeFps;
                    _videoFramesRendered = 0;
                    _fpsStopwatch.Restart();
                }
            }
            catch (Exception ex)
            {
                LogLoopError(ex);
                Thread.Yield();
            }
        }

        _logger.LogDebug("{Role} worker stopping.", worker.Role);
    }

    private void SessionLoop(PipelineWorker worker)
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            if (!worker.TryWaitIfPaused())
            {
                break;
            }

            try
            {
                ConsumePendingSeekTarget();
                LogSyncDiagnosticsIfDue(_clock.CurrentSeconds);

                if (_endOfStreamTracker.HasReachedEnd(HasPendingPlayableContent()))
                {
                    HandlePlaybackEnded();
                }
            }
            catch (Exception ex)
            {
                LogLoopError(ex);
            }

            Thread.Yield();
        }

        _logger.LogDebug("{Role} worker stopping.", worker.Role);
    }

    private void ConsumePendingSeekTarget()
    {
        lock (_seekSync)
        {
            if (_pendingSeekTargetSeconds.HasValue)
            {
                var targetSeconds = _pendingSeekTargetSeconds.Value;
                _audioPipelines.ForEach(p => p.DiscardBefore(targetSeconds));

                if (_audioPipelines.Count == 0 || _audioPipelines.All(p => p.HasBufferedAudio))
                {
                    _pendingSeekTargetSeconds = null;
                }
            }
        }
    }

    private bool HasPendingPlayableContent()
    {
        if (_videoPipeline is not null && _videoPipeline.TryPeek(out _))
        {
            return true;
        }

        return _audioPipelines.Any(p => p.HasBufferedAudio);
    }

    private void HandlePlaybackEnded()
    {
        Pause();
        _clock.Rebase(Duration.TotalSeconds);

        _logger.LogInformation("Reached end of stream for {FileName}.", FileName);
        PlaybackEnded?.Invoke();
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

    private void LogSyncDiagnosticsIfDue(double clockSeconds)
    {
        var hasAnythingToReport = _audioPipelines.Count > 0 || _videoPipeline is not null;
        if (!hasAnythingToReport || _syncLogStopwatch.ElapsedMilliseconds < SyncLogIntervalMs)
        {
            return;
        }

        _syncLogStopwatch.Restart();

        var parts = new string[_audioPipelines.Count];
        for (var i = 0; i < _audioPipelines.Count; i++)
        {
            var outputSeconds = _audioPipelines[i].OutputTimeSeconds;
            var outputId = i < _audioRoutes.Count ? _audioRoutes[i].Output.Id : -1;
            var expectedSeconds = clockSeconds - GetOutputDelaySeconds(outputId) * Speed;
            var driftMs = (outputSeconds - expectedSeconds) * 1000;
            var friendlyName = i < _audioRoutes.Count ? _audioRoutes[i].Output.FriendlyName : "?";
            var p = _audioPipelines[i];
            parts[i] = string.Create(
                CultureInfo.InvariantCulture,
                $"{friendlyName}={outputSeconds:F3}s(drift={driftMs:F0}ms,pktQ={p.DiagPacketQueueCount}," +
                $"pktDrop={p.DiagPacketDropCount},pcmQ={p.DiagDecodedQueueCount},total={p.DiagTotalChunksDecoded}," +
                $"skipBefore={p.DiagSkipBeforeSeconds:F3},cur={p.DiagCurrentTimeSeconds:F3}," +
                $"ptsBaseline={p.DiagPtsBaselineOffsetSeconds:F3}," +
                $"buf={p.DiagBufferedBytes}/{p.DiagBufferLength},front={p.DiagFrontChunkSeconds:F3})");
        }

        var videoPart = _videoPipeline is null
            ? "none"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"fps={VideoFps:F1},decodeFps={VideoDecodeFps:F1},frameQ={_videoPipeline.DiagFrameQueueCount}," +
                $"pktQ={_videoChannel.Reader.Count},genMismatch={_diagVideoGenerationMismatchCount}");

        _logger.LogDebug(
            "Sync: clock={ClockSeconds:F3}s, video=[{Video}], outputs=[{Outputs}].",
            clockSeconds,
            videoPart,
            string.Join(", ", parts));
    }

    private void ApplyVolumeToPipelines()
    {
        for (var i = 0; i < _audioPipelines.Count && i < _audioRoutes.Count; i++)
        {
            _audioPipelines[i].SetAmplitude(GetEffectiveAmplitude(_audioRoutes[i].Output.Id));
        }
    }

    private void ApplyDelayToPipelines()
    {
        for (var i = 0; i < _audioPipelines.Count && i < _audioRoutes.Count; i++)
        {
            _audioPipelines[i].SetDelay(GetOutputDelaySeconds(_audioRoutes[i].Output.Id));
        }
    }

    private double GetOutputDelaySeconds(int outputId) => _outputDelays.GetValueOrDefault(outputId, 0);

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

    private void ClearSubtitlePipelines()
    {
        _subtitlePipelines.ForEach(p => p.Dispose());
        _subtitlePipelines.Clear();
        _subtitleRoutes.Clear();
    }

    private static void DrainPacketChannel(Channel<PacketRef> channel)
    {
        while (channel.Reader.TryRead(out var packetRef))
        {
            var packet = packetRef.Packet;
            ffmpeg.av_packet_free(&packet);
        }
    }

    private bool TrySeekToTarget(double targetSeconds, out int result)
    {
        var seekStreamIndex = _videoPipeline?.StreamIndex ?? _audioPipelines[0].StreamIndex;

        if (_videoPipeline is null && targetSeconds < ZeroSeekEpsilonSeconds)
        {
            result = ffmpeg.av_seek_frame(_formatContext, -1, 0, ffmpeg.AVSEEK_FLAG_BYTE);
            if (result >= 0)
            {
                ffmpeg.avformat_flush(_formatContext);
                return true;
            }
        }

        var stream = _formatContext->streams[seekStreamIndex];
        var targetPtsInStreamTimeBase = (long)Math.Round(targetSeconds / ffmpeg.av_q2d(stream->time_base));
        result = ffmpeg.av_seek_frame(
            _formatContext,
            seekStreamIndex,
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