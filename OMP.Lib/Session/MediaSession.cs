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

    public TimeSpan Duration => PrimarySource.Duration;

    public string FileName { get; }

    public string FilePath { get; }

    public bool IsMuted { get; private set; }

    public double MasterVolume { get; private set; } = 1.0;

    public double Speed => _clock.Speed;

    public bool HasVideo => _videoPipeline is not null;

    public double VideoFps { get; private set; }

    public double VideoDecodeFps { get; private set; }

    private MediaInputSource PrimarySource => _sources[0];

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    private string? _lastLoopErrorMessage;
    private readonly Stopwatch _loopErrorStopwatch = Stopwatch.StartNew();
    private int _suppressedLoopErrors;

    private readonly List<MediaInputSource> _sources = [];
    private readonly Dictionary<int, (int SourceId, int LocalStreamIndex)> _audioStreamLocations = [];

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

    private readonly PipelineWorker? _videoWorker;
    private readonly PipelineWorker? _videoRenderWorker;
    private readonly PipelineWorker _subtitleWorker;
    private readonly PipelineWorker _sessionWorker;
    private readonly Lock _seekSync = new();

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
    private int _diagVideoGenerationMismatchCount;

    private const double MaxFrameLagSeconds = 0.05;
    private const double EarlyFrameWaitThresholdSeconds = 0.03;
    private const double SeekLookbackSeconds = 1;
    private const double LoopErrorLogIntervalMs = 5000;
    private const double SyncLogIntervalMs = 1000;
    private const double MaxDemuxLookaheadSeconds = 3;

    public MediaSession(
        MediaOpenRequest request,
        PlaybackTuningOptions options,
        ILoggerFactory loggerFactory,
        NativeLibraryOptions nativeLibraryOptions)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MediaSession>();

        FFmpegEnvironment.EnsureInitialized(_logger, nativeLibraryOptions.FFmpegLibraryDirectory);

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

        var primary = new MediaInputSource(
            0, request.PrimarySource, loggerFactory, cancellationToken: _cancellationTokenSource.Token);
        _sources.Add(primary);

        foreach (var sidecar in request.AudioSidecars)
        {
            try
            {
                _sources.Add(
                    new MediaInputSource(
                        _sources.Count,
                        sidecar.Url,
                        loggerFactory,
                        sidecar.Language,
                        sidecar.Title,
                        _cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not open audio sidecar {Url}; skipping.", sidecar.Url);
            }
        }

        FileName = Path.GetFileName(request.PrimarySource);
        FilePath = request.PrimarySource;

        for (var i = 0; i < primary.FormatContext->nb_streams; i++)
        {
            var stream = primary.FormatContext->streams[i];
            if (stream->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                _videoPipeline = new VideoPipeline(primary.FormatContext, i, loggerFactory, _cancellationTokenSource.Token);
                break;
            }
        }

        AudioStreams = BuildAudioCatalog();
        var outputScanner = new OutputScanner(loggerFactory);
        AudioOutputs = outputScanner.ScanOutputs();
        AudioOutputUnavailableReason = outputScanner.UnavailableReason;
        SubtitleStreams = new SubtitleScanner(loggerFactory).GetSubtitleStreams(primary.FormatContext);

        _subtitleWorker = new PipelineWorker(PipelineWorkerRole.Subtitle, _cancellationTokenSource.Token);
        _sessionWorker = new PipelineWorker(PipelineWorkerRole.Session, _cancellationTokenSource.Token);

        _subtitleWorker.Pause();
        _sessionWorker.Pause();

        foreach (var source in _sources)
        {
            source.DemuxWorker.Start(worker => DemuxLoop(source, worker), $"{PipelineWorkerRole.Demux}-{source.SourceId}");
        }

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
            "Opened {FilePath}: duration {Duration:c}, {AudioStreamCount} audio stream(s) across {SourceCount} " +
            "source(s), {SubtitleStreamCount} subtitle stream(s), {OutputCount} output(s), video={HasVideo}.",
            FilePath,
            Duration,
            AudioStreams.Count,
            _sources.Count,
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
            if (!_audioStreamLocations.TryGetValue(route.Stream.Id, out var location))
            {
                _logger.LogError(
                    "Could not resolve audio stream {StreamId} to a source; skipping route to '{FriendlyName}'.",
                    route.Stream.Id,
                    route.Output.FriendlyName);
                continue;
            }

            var source = _sources[location.SourceId];

            AudioPipeline pipeline;
            lock (source.FormatSync)
            {
                try
                {
                    pipeline = new AudioPipeline(
                        source.FormatContext,
                        location.LocalStreamIndex,
                        source.SourceId,
                        route.Output,
                        _audioBufferDurationSeconds,
                        _audioPacketChannelCapacity,
                        () => Volatile.Read(ref _seekGeneration),
                        () => _clock.CurrentSeconds,
                        _loggerFactory,
                        _cancellationTokenSource.Token);
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

            lock (PrimarySource.FormatSync)
            {
                _subtitlePipelines.Add(
                    new SubtitlePipeline(
                        PrimarySource.FormatContext,
                        route.Stream.Id,
                        PrimarySource.SourceId,
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

        _sources.ForEach(s => s.DemuxWorker.Resume());
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

        _sources.ForEach(s => s.DemuxWorker.Pause());
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

            var anySeeked = false;

            foreach (var source in _sources)
            {
                var referenceStreamIndex = GetSeekReferenceStreamIndex(source);
                if (referenceStreamIndex is null)
                {
                    continue;
                }

                bool seeked;
                int seekResult;
                lock (source.FormatSync)
                {
                    seeked = source.TrySeek(
                        seekTargetSeconds, referenceStreamIndex.Value, IsSourceAudioOnly(source), out seekResult);
                }

                if (!seeked)
                {
                    _logger.LogWarning(
                        "Seek to {TargetSeconds:F3}s failed for source {SourceId}: {Error}.",
                        targetSeconds,
                        source.SourceId,
                        FFmpegError.Describe(seekResult));
                    continue;
                }

                anySeeked = true;
                source.ResetPtsBaseline(IsSourceAudioOnly(source) ? seekTargetSeconds : 0);
                source.EndOfStreamTracker.MarkStreamReadable();
            }

            if (anySeeked)
            {
                _clock.Rebase(targetSeconds);
                _pendingSeekTargetSeconds = targetSeconds;
                _lastSeekTargetSeconds = targetSeconds;

                _audioPipelines.ForEach(pipeline =>
                {
                    var anchorSeconds = IsSourceAudioOnly(_sources[pipeline.SourceId]) ? seekTargetSeconds : 0;
                    pipeline.Flush();
                    pipeline.ResetClock(targetSeconds, anchorSeconds);
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

        _sources.ForEach(s => s.DemuxWorker.Join());
        _videoWorker?.Join();
        _videoRenderWorker?.Join();
        _subtitleWorker.Join();
        _sessionWorker.Join();

        _sources.ForEach(s => s.DemuxWorker.Dispose());
        _videoWorker?.Dispose();
        _videoRenderWorker?.Dispose();
        _subtitleWorker.Dispose();
        _sessionWorker.Dispose();

        VideoFrameReady = null;
        PlaybackEnded = null;
        ClearAudioPipelines();
        ClearSubtitlePipelines();
        _videoPipeline?.Dispose();

        _sources.ForEach(s => s.Dispose());
    }

    private List<AudioStream> BuildAudioCatalog()
    {
        var result = new List<AudioStream>();
        var scanner = new AudioScanner(_loggerFactory);
        var nextId = 0;

        foreach (var source in _sources)
        {
            foreach (var local in scanner.GetAudioStreams(source.FormatContext))
            {
                var globalId = nextId++;
                _audioStreamLocations[globalId] = (source.SourceId, local.Id);
                result.Add(
                    local with
                    {
                        Id = globalId,
                        Title = source.Title ?? local.Title,
                        Language = source.Language ?? local.Language
                    });
            }
        }

        return result;
    }

    private bool IsSourceAudioOnly(MediaInputSource source) => !(source.IsPrimary && _videoPipeline is not null);

    private int? GetSeekReferenceStreamIndex(MediaInputSource source)
    {
        if (source.IsPrimary && _videoPipeline is not null)
        {
            return _videoPipeline.StreamIndex;
        }

        return _audioPipelines.FirstOrDefault(p => p.SourceId == source.SourceId)?.StreamIndex;
    }

    private void DemuxLoop(MediaInputSource source, PipelineWorker worker)
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
            lock (source.FormatSync)
            {
                readResult = ffmpeg.av_read_frame(source.FormatContext, packet);
            }

            if (readResult < 0)
            {
                _logger.LogWarning(
                    "Demux read failed for source {SourceId} at generation {Generation}: {Error} ({Code}).",
                    source.SourceId,
                    Volatile.Read(ref _seekGeneration),
                    FFmpegError.Describe(readResult),
                    readResult);
                source.EndOfStreamTracker.MarkEndOfStream();
                worker.Pause();
                continue;
            }

            source.EndOfStreamTracker.MarkStreamReadable();

            var streamIndex = packet->stream_index;

            var generation = Volatile.Read(ref _seekGeneration);

            if (packet->pts != ffmpeg.AV_NOPTS_VALUE)
            {
                var packetSeconds = packet->pts * ffmpeg.av_q2d(source.FormatContext->streams[streamIndex]->time_base);
                var baselineOffset = source.GetOrDetectPtsBaselineOffset(streamIndex, packetSeconds);

                ThrottleDemuxAhead(packetSeconds + baselineOffset, generation, worker);
            }

            foreach (var pipeline in _audioPipelines)
            {
                if (pipeline.SourceId == source.SourceId && pipeline.StreamIndex == streamIndex)
                {
                    DispatchClonedPacket(packet, generation, pipeline.TryEnqueuePacket);
                }
            }

            if (source.IsPrimary && streamIndex == _videoPipeline?.StreamIndex)
            {
                DispatchClonedPacket(
                    packet,
                    generation,
                    packetRef => _videoChannel.Writer.TryWriteBlocking(packetRef, cancellationToken));
            }

            if (_subtitlePipelines.Any(pipeline => pipeline.SourceId == source.SourceId && pipeline.StreamIndex == streamIndex))
            {
                DispatchClonedPacket(packet, generation, _subtitleChannel.Writer.TryWrite);
            }

            ffmpeg.av_packet_unref(packet);
        }

        ffmpeg.av_packet_free(&packet);
        _logger.LogDebug("{Role} worker stopping for source {SourceId}.", worker.Role, source.SourceId);
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

                var hasPending = HasPendingPlayableContent();
                if (_sources.All(s => s.EndOfStreamTracker.HasReachedEnd(hasPending)))
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
}
