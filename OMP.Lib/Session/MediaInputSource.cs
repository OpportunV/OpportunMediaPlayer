using System.Runtime.InteropServices;
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

    /// <summary>
    /// True for anything FFmpeg reads through a network protocol rather than the local file system.
    /// </summary>
    public bool IsNetwork { get; }

    public AVFormatContext* FormatContext => _formatContext;

    /// <summary>
    /// Read once at open rather than on every access.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Track names/languages FFmpeg doesn't expose, keyed by <c>AVStream.id</c> (see
    /// <see cref="Mp4TrackMetadataReader"/>). Empty for anything but a local MP4/MOV file: a network
    /// source would need extra requests just for labels, and web sidecars get theirs from yt-dlp anyway.
    /// </summary>
    public IReadOnlyDictionary<int, Mp4TrackMetadata> Mp4Tracks { get; }

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

    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable To prevent GC
    private readonly AVIOInterruptCB_callback _interruptCallback;

    private const int InterruptTimeoutMs = 15000;
    private const double ZeroSeekEpsilonSeconds = 0.05;
    private const string ReconnectDelayMaxSeconds = "5";
    private const string MovDemuxerName = "mov";
    private const string FileProtocolPrefix = "file:";

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

        var protocol = ffmpeg.avio_find_protocol_name(url);
        IsNetwork = protocol is not null && protocol != "file";

        _formatContext = ffmpeg.avformat_alloc_context();
        _interruptCallback = InterruptCallback;
        _formatContext->interrupt_callback.callback = _interruptCallback;

        AVDictionary* openOptions = null;
        if (headers is { Count: > 0 })
        {
            var headerBlock = string.Concat(headers.Select(kv => $"{kv.Key}: {kv.Value}\r\n"));
            ffmpeg.av_dict_set(&openOptions, "headers", headerBlock, 0);
        }

        if (IsNetwork)
        {
            ffmpeg.av_dict_set(&openOptions, "reconnect", "1", 0);
            ffmpeg.av_dict_set(&openOptions, "reconnect_streamed", "1", 0);
            ffmpeg.av_dict_set(&openOptions, "reconnect_on_network_error", "1", 0);
            ffmpeg.av_dict_set(&openOptions, "reconnect_delay_max", ReconnectDelayMaxSeconds, 0);
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
                    "Reading stream info for {Url} timed out after {TimeoutMs}ms and was aborted",
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

        Duration = _formatContext->duration > 0
            ? TimeSpan.FromSeconds(_formatContext->duration / (double)ffmpeg.AV_TIME_BASE)
            : TimeSpan.Zero;

        Mp4Tracks = ReadMp4Tracks();

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

    private IReadOnlyDictionary<int, Mp4TrackMetadata> ReadMp4Tracks()
    {
        // FFmpeg's MP4 demuxer registers as "mov,mp4,m4a,3gp,3g2,mj2".
        var demuxerName = Marshal.PtrToStringUTF8((IntPtr)_formatContext->iformat->name) ?? string.Empty;
        if (IsNetwork || !demuxerName.Split(',').Contains(MovDemuxerName))
        {
            return new Dictionary<int, Mp4TrackMetadata>();
        }

        var path = Url.StartsWith(FileProtocolPrefix, StringComparison.Ordinal) ? Url[FileProtocolPrefix.Length..] : Url;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var tracks = Mp4TrackMetadataReader.Read(file);
            _logger.LogDebug("Read MP4 track metadata for {Count} track(s) of {Url}.", tracks.Count, Url);
            return tracks;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not read MP4 track metadata from {Url}.", Url);
            return new Dictionary<int, Mp4TrackMetadata>();
        }
    }
}
