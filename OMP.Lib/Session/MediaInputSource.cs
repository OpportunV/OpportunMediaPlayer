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

    private double _pendingPtsBaselineAnchorSeconds;
    private readonly AVFormatContext* _formatContext;
    private readonly Dictionary<int, double> _ptsBaselineOffsets = [];

    private const double ZeroSeekEpsilonSeconds = 0.05;

    public MediaInputSource(
        int sourceId,
        string url,
        ILoggerFactory loggerFactory,
        string? language = null,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        SourceId = sourceId;
        Url = url;
        Language = language;
        Title = title;

        var logger = loggerFactory.CreateLogger<MediaInputSource>();

        int openResult;
        fixed (AVFormatContext** fc = &_formatContext)
        {
            openResult = ffmpeg.avformat_open_input(fc, url, null, null);
        }

        if (openResult != 0)
        {
            logger.LogError("Could not open {Url}: {Error}.", url, FFmpegError.Describe(openResult));
            throw new ApplicationException("Could not open file.");
        }

        var streamInfoResult = ffmpeg.avformat_find_stream_info(_formatContext, null);
        if (streamInfoResult < 0)
        {
            logger.LogError(
                "Could not read stream info for {Url}: {Error}.", url, FFmpegError.Describe(streamInfoResult));
            throw new ApplicationException("Could not find stream info.");
        }

        DemuxWorker = new PipelineWorker(PipelineWorkerRole.Demux, cancellationToken);
        DemuxWorker.Pause();
    }

    public void Dispose()
    {
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

    public bool TrySeek(double targetSeconds, int referenceStreamIndex, bool isAudioOnly, out int result)
    {
        if (isAudioOnly && targetSeconds < ZeroSeekEpsilonSeconds)
        {
            result = ffmpeg.av_seek_frame(_formatContext, -1, 0, ffmpeg.AVSEEK_FLAG_BYTE);
            if (result >= 0)
            {
                ffmpeg.avformat_flush(_formatContext);
                return true;
            }
        }

        var stream = _formatContext->streams[referenceStreamIndex];
        var targetPtsInStreamTimeBase = (long)Math.Round(targetSeconds / ffmpeg.av_q2d(stream->time_base));
        result = ffmpeg.av_seek_frame(
            _formatContext,
            referenceStreamIndex,
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
