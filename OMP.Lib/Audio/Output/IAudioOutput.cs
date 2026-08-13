using NAudio.Wave;

namespace OMP.Lib.Audio.Output;

internal interface IAudioOutput : IDisposable
{
    public int PreferredSampleRate { get; }

    public double OutputLatencySeconds { get; }

    public void Init(IWaveProvider source);

    public void Play();

    public void Pause();
}
