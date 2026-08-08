using System.Runtime.InteropServices;

namespace OMP.Lib.Audio;

internal static class AudioGainProcessor
{
    private const float UnityAmplitude = 1f;

    public static double ToAmplitude(double normalizedVolume)
    {
        var clamped = Math.Clamp(normalizedVolume, AudioVolumeLimits.Min, AudioVolumeLimits.Max);
        return clamped <= UnityAmplitude ? clamped * clamped : clamped;
    }

    public static void Apply(byte[] pcm, int offset, int count, float amplitude)
    {
        if (count <= 0 || Math.Abs(amplitude - UnityAmplitude) < float.Epsilon)
        {
            return;
        }

        var alignedCount = count - count % sizeof(short);
        var samples = MemoryMarshal.Cast<byte, short>(pcm.AsSpan(offset, alignedCount));

        if (amplitude <= 0)
        {
            samples.Clear();
            return;
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Clamp(samples[i] * amplitude, short.MinValue, short.MaxValue);
        }
    }
}
