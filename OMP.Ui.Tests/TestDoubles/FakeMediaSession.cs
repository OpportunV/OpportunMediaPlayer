using OMP.Lib.Audio;
using OMP.Lib.Audio.Output;
using OMP.Lib.Session;
using OMP.Lib.Subtitle;
using OMP.Lib.Video;

namespace OMP.Ui.Tests.TestDoubles;

internal sealed class FakeMediaSession : IMediaSession
{
    public IReadOnlyList<AudioStream> AudioStreams { get; set; } = [];

    public IReadOnlyList<AudioOutput> AudioOutputs { get; set; } = [];

    public string? AudioOutputUnavailableReason { get; set; }

    public IReadOnlyList<AudioRoute> AudioRoutes { get; set; } = [];

    public IReadOnlyList<SubtitleStream> SubtitleStreams { get; set; } = [];

    public IReadOnlyList<SubtitleRoute> SubtitleRoutes { get; set; } = [];

    public IReadOnlyDictionary<int, OutputVolumeState> OutputVolumes { get; set; } =
        new Dictionary<int, OutputVolumeState>();

    public IReadOnlyDictionary<int, double> OutputDelays { get; set; } = new Dictionary<int, double>();

    public TimeSpan CurrentTime { get; set; }

    public TimeSpan Duration { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public bool HasVideo { get; set; }

    public bool IsMuted { get; private set; }

    public double MasterVolume { get; private set; } = 1.0;

    public double Speed { get; private set; } = 1.0;

    public double VideoFps { get; set; }

    public double VideoDecodeFps { get; set; }

    public int PlayCallCount { get; private set; }

    public int PauseCallCount { get; private set; }

    public TimeSpan? LastSeekTarget { get; private set; }

    public TimeSpan? LastStepOffset { get; private set; }

    public event Action<VideoFrame>? VideoFrameReady;

    public event Action? PlaybackEnded;

    public void SetAudioRoutes(IEnumerable<AudioRoute> routes)
    {
    }

    public void SetSubtitleRoutes(IEnumerable<SubtitleRoute> routes)
    {
    }

    public IReadOnlyList<SubtitleCue> GetActiveSubtitleCues() => [];

    public void Play() => PlayCallCount++;

    public void Pause() => PauseCallCount++;

    public void Step(TimeSpan offset) => LastStepOffset = offset;

    public void Seek(TimeSpan target) => LastSeekTarget = target;

    public void SetSpeed(double speed) => Speed = speed;

    public void SetMasterVolume(double volume) => MasterVolume = volume;

    public void SetMasterMuted(bool muted) => IsMuted = muted;

    public void SetOutputVolume(int outputId, double volume)
    {
    }

    public void SetOutputMuted(int outputId, bool muted)
    {
    }

    public void SetOutputDelay(int outputId, double delaySeconds)
    {
    }

    public void RaiseVideoFrameReady(VideoFrame frame) => VideoFrameReady?.Invoke(frame);

    public void RaisePlaybackEnded() => PlaybackEnded?.Invoke();

    public void Dispose()
    {
    }
}
