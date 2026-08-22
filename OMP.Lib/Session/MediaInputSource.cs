using FFmpeg.AutoGen;
using Microsoft.Extensions.Logging;
using OMP.Lib.Audio;
using OMP.Lib.Interop;
using OMP.Lib.Threading;

namespace OMP.Lib.Session;

internal sealed unsafe class MediaInputSource : IDisposable
{
    public int SourceId { get; }

    public bool IsPrimary => SourceId == 0;

    public string? Language { get; }

    public string? Title { get; }

    public string Url { get; }

    public AVFormatContext* FormatContext => _formatContext;

    public TimeSpan Duration
    {
        get
        {
            lock (FormatSync)
            {
                return _formatContext->duration > 0
                    ? TimeSpan.FromSeconds(_formatContext->duration / (double)ffmpeg.AV_TIME_BASE)
                    : TimeSpan.Zero;
            }
        }
    }

    public Lock FormatSync { get; } = new();

    public EndOfStreamTracker EndOfStreamTracker { get; } = new();

    public PipelineWorker DemuxWorker { get; }

    private long _interruptDeadlineTicks;
    private volatile bool _interruptFired;
    private double _pendingPtsBaselineAnchorSeconds;
    private AVFormatContext* _formatContext;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;
    private readonly Dictionary<int, double> _ptsBaselineOffsets = [];

    private readonly AVIOInterruptCB_callback _interruptCallback;

    private const int InterruptTimeoutMs = 15000;
    private const double ZeroSeekEpsilonSeconds = 0.05;

    public MediaInputSource(
        int sourceId,
        string url,
        ILoggerFactory loggerFactory,
        string? language = null,
        string? title = null,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        SourceId = sourceId;
        Url = url;
        Language = language;
        Title = title;

        _logger = loggerFactory.CreateLogger<MediaInputSource>();
        _cancellationToken = cancellationToken;

        _formatContext = ffmpeg.avformat_alloc_context();
        _interruptCallback = InterruptCallback;
        _formatContext->interrupt_callback.callback = _interruptCallback;

        AVDictionary* openOptions = null;
        if (headers is { Count: > 0 })
        {
            var headerBlock = string.Concat(headers.Select(kv => $"{kv.Key}: {kv.Value}\r\n"));
            ffmpeg.av_dict_set(&openOptions, "headers", headerBlock, 0);
        }

        ArmInterruptDeadline();

        int openResult;
        fixed (AVFormatContext** fc = &_formatContext)
        {
            openResult = ffmpeg.avformat_open_input(fc, url, null, &openOptions);
        }

        if (openOptions != null)
        {
            ffmpeg.av_dict_free(&openOptions);
        }

        if (openResult != 0)
        {
            if (ConsumeInterruptFired())
            {
                _logger.LogWarning(
                    "Open of {Url} timed out after {TimeoutMs}ms and was aborted.", url, InterruptTimeoutMs);
            }
            else
            {
                _logger.LogError("Could not open {Url}: {Error}.", url, FFmpegError.Describe(openResult));
            }

            throw new ApplicationException("Could not open file.");
        }

        ArmInterruptDeadline();
        var streamInfoResult = ffmpeg.avformat_find_stream_info(_formatContext, null);
        if (streamInfoResult < 0)
        {
            if (ConsumeInterruptFired())
            {
                _logger.LogWarning(
                    "Reading stream info for {Url} timed out after {TimeoutMs}ms and was aborted.",
                    url,
                    InterruptTimeoutMs);
            }
            else
            {
                _logger.LogError(
                    "Could not read stream info for {Url}: {Error}.", url, FFmpegError.Describe(streamInfoResult));
            }

            throw new ApplicationException("Could not find stream info.");
        }

        DemuxWorker = new PipelineWorker(PipelineWorkerRole.Demux, cancellationToken);
        DemuxWorker.Pause();
    }

    public void Dispose()
    {
        _logger.LogDebug("Disposing source {SourceId} ({Url}).", SourceId, Url);

        DemuxWorker.Join();
        DemuxWorker.Dispose();

        fixed (AVFormatContext** fc = &_formatContext)
        {
            ffmpeg.avformat_close_input(fc);
        }
    }

    public double GetOrDetectPtsBaselineOffset(int localStreamIndex, double packetSeconds)
    {
        if (_ptsBaselineOffsets.TryGetValue(localStreamIndex, out var offset))
        {
            return offset;
        }

        offset = PtsBaselineDetector.DetectOffset(packetSeconds, _pendingPtsBaselineAnchorSeconds);
        _ptsBaselineOffsets[localStreamIndex] = offset;
        return offset;
    }

    public void ResetPtsBaseline(double anchorSeconds)
    {
        _ptsBaselineOffsets.Clear();
        _pendingPtsBaselineAnchorSeconds = anchorSeconds;
    }

    public void ArmInterruptDeadline()
    {
        _interruptDeadlineTicks = Environment.TickCount64 + InterruptTimeoutMs;
        _interruptFired = false;
    }

    public bool ConsumeInterruptFired()
    {
        var fired = _interruptFired;
        _interruptFired = false;
        return fired;
    }

    public bool TrySeek(double targetSeconds, int referenceStreamIndex, bool isAudioOnly, out int result, out bool timedOut)
    {
        if (isAudioOnly && targetSeconds < ZeroSeekEpsilonSeconds)
        {
            ArmInterruptDeadline();
            result = ffmpeg.av_seek_frame(_formatContext, -1, 0, ffmpeg.AVSEEK_FLAG_BYTE);
            if (result >= 0)
            {
                ffmpeg.avformat_flush(_formatContext);
                timedOut = false;
                return true;
            }
        }

        var stream = _formatContext->streams[referenceStreamIndex];
        var targetPtsInStreamTimeBase = (long)Math.Round(targetSeconds / ffmpeg.av_q2d(stream->time_base));

        ArmInterruptDeadline();
        result = ffmpeg.av_seek_frame(
            _formatContext,
            referenceStreamIndex,
            targetPtsInStreamTimeBase,
            ffmpeg.AVSEEK_FLAG_BACKWARD);

        if (result >= 0)
        {
            ffmpeg.avformat_flush(_formatContext);
            timedOut = false;
            return true;
        }

        timedOut = ConsumeInterruptFired();
        return false;
    }

    private int InterruptCallback(void* opaque)
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            _interruptFired = true;
            return 1;
        }

        if (Environment.TickCount64 <= _interruptDeadlineTicks)
        {
            return 0;
        }

        _interruptFired = true;
        return 1;
    }
}
