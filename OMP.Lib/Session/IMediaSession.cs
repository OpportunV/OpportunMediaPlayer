using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Subtitle;
using OMP.Lib.Video;

namespace OMP.Lib.Session;

public interface IMediaSession : IDisposable
{
    public event Action<VideoFrame>? VideoFrameReady;

    public event Action? PlaybackEnded;

    public IReadOnlyList<AudioStream> AudioStreams { get; }

    public IReadOnlyList<AudioOutput> AudioOutputs { get; }

    public string? AudioOutputUnavailableReason { get; }

    public IReadOnlyList<AudioRoute> AudioRoutes { get; }

    public IReadOnlyList<SubtitleStream> SubtitleStreams { get; }

    public IReadOnlyList<SubtitleRoute> SubtitleRoutes { get; }

    public IReadOnlyDictionary<int, OutputVolumeState> OutputVolumes { get; }

    public IReadOnlyDictionary<int, double> OutputDelays { get; }

    public TimeSpan CurrentTime { get; }

    public TimeSpan Duration { get; }

    public string FileName { get; }

    public string FilePath { get; }

    public bool HasVideo { get; }

    public bool IsMuted { get; }

    public double MasterVolume { get; }

    public double Speed { get; }

    public double VideoFps { get; }

    public double VideoDecodeFps { get; }

    public void SetAudioRoutes(IEnumerable<AudioRoute> routes);

    public IReadOnlyList<SubtitleRoute> SetSubtitleRoutes(IEnumerable<SubtitleRoute> routes);

    public SubtitleStream AddSubtitleSidecar(SubtitleSidecarSource sidecar);

    public IReadOnlyList<SubtitleCue> GetActiveSubtitleCues();

    public void Play();

    public void Pause();

    public void Step(TimeSpan offset);

    public void Seek(TimeSpan target);

    public void SetSpeed(double speed);

    public void SetMasterVolume(double volume);

    public void SetMasterMuted(bool muted);

    public void SetOutputVolume(int outputId, double volume);

    public void SetOutputMuted(int outputId, bool muted);

    public void SetOutputDelay(int outputId, double delaySeconds);
}
