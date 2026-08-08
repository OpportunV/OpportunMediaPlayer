using NAudio.Wave;

namespace OMP.Lib.Audio;

internal sealed class GainWaveProvider(IWaveProvider source) : IWaveProvider
{
    public WaveFormat WaveFormat => source.WaveFormat;

    public float Amplitude
    {
        get => _amplitude;
        set => _amplitude = value;
    }

    private volatile float _amplitude = 1f;

    public int Read(byte[] buffer, int offset, int count)
    {
        var read = source.Read(buffer, offset, count);
        AudioGainProcessor.Apply(buffer, offset, read, _amplitude);
        return read;
    }
}
