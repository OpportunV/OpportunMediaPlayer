using FFmpeg.AutoGen;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;

namespace OMP.Lib.Session;

public sealed unsafe class MediaSession : IMediaSession
{
    public IReadOnlyList<AudioStream> AudioStreams { get; }

    public IReadOnlyList<AudioOutput> AudioOutputs { get; }

    public TimeSpan CurrentTime => _audioPipelines.Count > 0 ? TimeSpan.FromSeconds(_audioPipelines[0].CurrentTimeSeconds) : TimeSpan.Zero;

    public TimeSpan Duration => _formatContext->duration > 0 ? TimeSpan.FromSeconds(_formatContext->duration / (double)ffmpeg.AV_TIME_BASE) : TimeSpan.Zero;

    private readonly List<AudioPipeline> _audioPipelines = [];

    private readonly Thread _demuxThread;

    private readonly AVFormatContext* _formatContext;

    private volatile bool _paused = true;
    private volatile bool _running = true;

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

        AudioStreams = new AudioScanner().GetAudioStreams(_formatContext);
        AudioOutputs = new OutputScanner().ScanOutputs();

        _demuxThread = new Thread(DemuxLoop)
        {
            IsBackground = true
        };

        _demuxThread.Start();
    }

    public void SetAudioRoutes(IEnumerable<(AudioStream stream, AudioOutput output)> routes)
    {
        ClearAudioPipelines();

        foreach (var (stream, output) in routes)
        {
            _audioPipelines.Add(new AudioPipeline(_formatContext, stream.Id, output.Id));
        }
    }

    public void Play()
    {
        _paused = false;
        _audioPipelines.ForEach(p => p.Play());
    }

    public void Pause()
    {
        _paused = true;
        _audioPipelines.ForEach(p => p.Pause());
    }

    public void Step(TimeSpan offset)
    {
        if (_audioPipelines.Count == 0)
        {
            return;
        }

        var wasPlaying = !_paused;
        Pause();
        var audioPipeline = _audioPipelines[0];
        var stream = _formatContext->streams[audioPipeline.StreamIndex];
        var targetSeconds = CurrentTime.TotalSeconds + offset.TotalSeconds;
        if (targetSeconds < 0)
        {
            targetSeconds = 0;
        }

        var targetPts = (long)Math.Round(targetSeconds / ffmpeg.av_q2d(stream->time_base));

        if (ffmpeg.av_seek_frame(
                _formatContext,
                audioPipeline.StreamIndex,
                targetPts,
                ffmpeg.AVSEEK_FLAG_BACKWARD) < 0)
        {
            Console.WriteLine("Error during seek.");
            return;
        }

        ffmpeg.avformat_flush(_formatContext);

        foreach (var pipeline in _audioPipelines)
        {
            pipeline.Flush();
        }

        if (wasPlaying)
        {
            Play();
        }
    }

    public void Dispose()
    {
        _running = false;
        _paused = false;
        _demuxThread.Join();

        ClearAudioPipelines();

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

            if (ffmpeg.av_read_frame(_formatContext, packet) < 0)
            {
                break;
            }

            foreach (var pipeline in _audioPipelines)
            {
                if (pipeline.StreamIndex == packet->stream_index)
                {
                    pipeline.Enqueue(packet);
                }
            }

            ffmpeg.av_packet_unref(packet);
        }

        ffmpeg.av_packet_free(&packet);
    }

    private void ClearAudioPipelines()
    {
        _audioPipelines.ForEach(p => p.Dispose());
        _audioPipelines.Clear();
    }
}