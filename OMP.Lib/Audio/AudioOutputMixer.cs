using OMP.Lib.Audio.Output;

namespace OMP.Lib.Audio;

internal sealed class AudioOutputMixer
{
    public bool IsMuted { get; private set; }

    public double MasterVolume { get; private set; } = 1.0;

    public IReadOnlyDictionary<int, OutputVolumeState> Volumes => _volumes.AsReadOnly();

    public IReadOnlyDictionary<int, double> Delays => _delays.AsReadOnly();

    private readonly Dictionary<int, OutputVolumeState> _volumes = [];
    private readonly Dictionary<int, double> _delays = [];

    public void SetMasterVolume(double volume) =>
        MasterVolume = Math.Clamp(volume, AudioVolumeLimits.Min, AudioVolumeLimits.Max);

    public void SetMasterMuted(bool muted) => IsMuted = muted;

    public void SetOutputVolume(int outputId, double volume) =>
        _volumes[outputId] = GetVolumeState(outputId)
            with { Volume = Math.Clamp(volume, AudioVolumeLimits.Min, AudioVolumeLimits.Max) };

    public void SetOutputMuted(int outputId, bool muted) =>
        _volumes[outputId] = GetVolumeState(outputId) with { Muted = muted };

    public void SetOutputDelay(int outputId, double delaySeconds) =>
        _delays[outputId] = Math.Clamp(delaySeconds, AudioDelayLimits.Min, AudioDelayLimits.Max);

    public float GetEffectiveAmplitude(int outputId)
    {
        var state = GetVolumeState(outputId);

        if (IsMuted || state.Muted)
        {
            return 0f;
        }

        return (float)(AudioGainProcessor.ToAmplitude(MasterVolume) * AudioGainProcessor.ToAmplitude(state.Volume));
    }

    public double GetDelaySeconds(int outputId) => _delays.GetValueOrDefault(outputId, 0);

    private OutputVolumeState GetVolumeState(int outputId) =>
        _volumes.TryGetValue(outputId, out var state) ? state : new OutputVolumeState(1.0, false);
}
