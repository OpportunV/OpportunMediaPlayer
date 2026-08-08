using OMP.Lib.Audio;

namespace OMP.Lib.Tests;

public class AudioGainProcessorTests
{
    [Fact]
    public void ToAmplitude_AtEnds_IsIdentity()
    {
        Assert.Equal(0, AudioGainProcessor.ToAmplitude(AudioVolumeLimits.Min));
        Assert.Equal(AudioVolumeLimits.Max, AudioGainProcessor.ToAmplitude(AudioVolumeLimits.Max));
    }

    [Fact]
    public void ToAmplitude_AtUnity_IsContinuousAcrossTheCurveBoundary()
    {
        Assert.Equal(1, AudioGainProcessor.ToAmplitude(1));
    }

    [Fact]
    public void ToAmplitude_BelowUnity_IsMonotonicAndBelowLinear()
    {
        var previous = -1.0;

        for (var volume = 0.0; volume <= 1.0; volume += 0.05)
        {
            var amplitude = AudioGainProcessor.ToAmplitude(volume);

            Assert.True(amplitude > previous);
            Assert.True(amplitude <= volume);
            previous = amplitude;
        }
    }

    [Fact]
    public void ToAmplitude_AboveUnity_IsLinearBoost()
    {
        Assert.Equal(1.5, AudioGainProcessor.ToAmplitude(1.5), precision: 10);
        Assert.Equal(AudioVolumeLimits.Max, AudioGainProcessor.ToAmplitude(AudioVolumeLimits.Max), precision: 10);
    }

    [Fact]
    public void ToAmplitude_OutOfRange_ClampsToVolumeLimits()
    {
        Assert.Equal(0, AudioGainProcessor.ToAmplitude(-5));
        Assert.Equal(AudioVolumeLimits.Max, AudioGainProcessor.ToAmplitude(5));
    }

    [Fact]
    public void Apply_AtUnity_IsByteForBytePassthrough()
    {
        var pcm = MakeSamples(1000, -1000, short.MaxValue, short.MinValue);
        var expected = pcm.ToArray();

        AudioGainProcessor.Apply(pcm, 0, pcm.Length, 1f);

        Assert.Equal(expected, pcm);
    }

    [Fact]
    public void Apply_AtZero_SilencesEverySample()
    {
        var pcm = MakeSamples(1000, -1000, short.MaxValue, short.MinValue);

        AudioGainProcessor.Apply(pcm, 0, pcm.Length, 0f);

        Assert.All(ReadSamples(pcm), sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void Apply_AtHalf_HalvesMagnitudes()
    {
        var pcm = MakeSamples(1000, -1000, 20000, -20000);

        AudioGainProcessor.Apply(pcm, 0, pcm.Length, 0.5f);

        var samples = ReadSamples(pcm);
        Assert.Equal(500, samples[0]);
        Assert.Equal(-500, samples[1]);
        Assert.Equal(10000, samples[2]);
        Assert.Equal(-10000, samples[3]);
    }

    [Fact]
    public void Apply_AtFullScale_DoesNotWrapSign()
    {
        var pcm = MakeSamples(short.MinValue, short.MaxValue);

        AudioGainProcessor.Apply(pcm, 0, pcm.Length, 1f);

        var samples = ReadSamples(pcm);
        Assert.Equal(short.MinValue, samples[0]);
        Assert.Equal(short.MaxValue, samples[1]);
    }

    [Fact]
    public void Apply_BoostBeyondUnity_ClipsWithoutWrappingSign()
    {
        var pcm = MakeSamples(short.MinValue, short.MaxValue, 20000, -20000);

        AudioGainProcessor.Apply(pcm, 0, pcm.Length, 2f);

        var samples = ReadSamples(pcm);
        Assert.Equal(short.MinValue, samples[0]);
        Assert.Equal(short.MaxValue, samples[1]);
        Assert.Equal(short.MaxValue, samples[2]);
        Assert.Equal(short.MinValue, samples[3]);
    }

    [Fact]
    public void Apply_WithOddCount_LeavesTrailingByteUntouched()
    {
        var pcm = MakeSamples(1000, -1000);
        var trailing = pcm[^1];

        AudioGainProcessor.Apply(pcm, 0, pcm.Length - 1, 0.5f);

        Assert.Equal(500, ReadSamples(pcm)[0]);
        Assert.Equal(trailing, pcm[^1]);
    }

    [Fact]
    public void Apply_RespectsOffsetAndCount()
    {
        var pcm = MakeSamples(1000, 2000, 3000, 4000);

        AudioGainProcessor.Apply(pcm, sizeof(short), sizeof(short) * 2, 0.5f);

        var samples = ReadSamples(pcm);
        Assert.Equal(1000, samples[0]);
        Assert.Equal(1000, samples[1]);
        Assert.Equal(1500, samples[2]);
        Assert.Equal(4000, samples[3]);
    }

    [Fact]
    public void Apply_WithNonPositiveCount_DoesNothing()
    {
        var pcm = MakeSamples(1000, -1000);
        var expected = pcm.ToArray();

        AudioGainProcessor.Apply(pcm, 0, 0, 0f);

        Assert.Equal(expected, pcm);
    }

    private static byte[] MakeSamples(params short[] samples)
    {
        var pcm = new byte[samples.Length * sizeof(short)];

        for (var i = 0; i < samples.Length; i++)
        {
            BitConverter.GetBytes(samples[i]).CopyTo(pcm, i * sizeof(short));
        }

        return pcm;
    }

    private static short[] ReadSamples(byte[] pcm)
    {
        var samples = new short[pcm.Length / sizeof(short)];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BitConverter.ToInt16(pcm, i * sizeof(short));
        }

        return samples;
    }
}
