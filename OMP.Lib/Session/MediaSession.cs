using FFmpeg.AutoGen;
using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Video;

namespace OMP.Lib.Session;

public sealed unsafe class MediaSession : IMediaSession
{
    public IReadOnlyList<AudioStream> AudioStreams { get; }

    public IReadOnlyList<AudioOutput> AudioOutputs { get; }

    public IReadOnlyList<(AudioStream audioStream, AudioOutput audioOutput)> AudioRoutes => _audioRoutes.AsReadOnly();

    public TimeSpan CurrentTime => _audioPipelines.Count > 0 ? TimeSpan.FromSeconds(_audioPipelines[0].CurrentTimeSeconds) : TimeSpan.Zero;

    public TimeSpan Duration => _formatContext->duration > 0 ? TimeSpan.FromSeconds(_formatContext->duration / (double)ffmpeg.AV_TIME_BASE) : TimeSpan.Zero;

    public VideoFrame? VideoFrame => _videoPipeline?.Frame;

    private readonly List<AudioPipeline> _audioPipelines = [];
    private readonly List<(AudioStream audioStream, AudioOutput audioOutput)> _audioRoutes = [];
    private VideoPipeline? _videoPipeline;

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

        _demuxThread = new Thread(DemuxLoop)
        {
            IsBackground = true
        };

        _demuxThread.Start();
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
            _audioPipelines.Add(new AudioPipeline(_formatContext, stream.Id, output.Id));
        }

        if (wasPlaying)
        {
            Play();
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
        var targetSeconds = CurrentTime + offset;
        Seek(targetSeconds);
    }

    public void Seek(TimeSpan target)
    {
        if (_audioPipelines.Count == 0)
        {
            return;
        }

        var targetSeconds = Math.Clamp(target.TotalSeconds, 0, Duration.TotalSeconds);
        var audioPipeline = _audioPipelines[0];
        var stream = _formatContext->streams[audioPipeline.StreamIndex];
        var wasPlaying = !_paused;
        Pause();

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

        _audioPipelines.ForEach(pipeline => pipeline.Flush());
        _videoPipeline?.Flush();

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
            
            if (_videoPipeline != null && packet->stream_index == _videoPipeline.StreamIndex)
            {
                _videoPipeline.Enqueue(packet);
            }

            ffmpeg.av_packet_unref(packet);
        }

        ffmpeg.av_packet_free(&packet);
    }

    private void ClearAudioPipelines()
    {
        _audioPipelines.ForEach(p => p.Dispose());
        _audioPipelines.Clear();
        _audioRoutes.Clear();
    }
}