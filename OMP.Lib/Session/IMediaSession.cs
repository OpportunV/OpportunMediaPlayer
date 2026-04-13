using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Video;

namespace OMP.Lib.Session;

public interface IMediaSession : IDisposable
{
    public event Action<VideoFrame>? VideoFrameReady;

    public IReadOnlyList<AudioStream> AudioStreams { get; }

    public IReadOnlyList<AudioOutput> AudioOutputs { get; }

    public IReadOnlyList<(AudioStream audioStream, AudioOutput audioOutput)> AudioRoutes { get; }

    public TimeSpan CurrentTime { get; }

    public TimeSpan Duration { get; }

    public string FileName { get; }

    public double Speed { get; }

    public double VideoFps { get; }

    public double VideoDecodeFps { get; }

    public void SetAudioRoutes(IEnumerable<(AudioStream stream, AudioOutput output)> routes);

    public void Play();

    public void Pause();

    public void Step(TimeSpan offset);

    public void Seek(TimeSpan target);

    public void SetSpeed(double speed);
}