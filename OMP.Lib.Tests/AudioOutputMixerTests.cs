using OMP.Lib.Audio;

namespace OMP.Lib.Tests;

public class AudioOutputMixerTests
{
    private const int OutputId = 7;

    [Fact]
    public void NewMixer_IsUnmutedAtFullVolumeWithNoDelay()
    {
        var mixer = new AudioOutputMixer();

        Assert.False(mixer.IsMuted);
        Assert.Equal(1.0, mixer.MasterVolume);
        Assert.Equal(0, mixer.GetDelaySeconds(OutputId));
    }

    [Fact]
    public void GetEffectiveAmplitude_UntouchedOutput_DefaultsToUnity()
    {
        var mixer = new AudioOutputMixer();

        Assert.Equal(1f, mixer.GetEffectiveAmplitude(OutputId), 5);
    }

    [Fact]
    public void GetEffectiveAmplitude_MultipliesMasterAndOutputTapers()
    {
        var mixer = new AudioOutputMixer();
        mixer.SetMasterVolume(0.5);
        mixer.SetOutputVolume(OutputId, 0.5);

        Assert.Equal(0.0625f, mixer.GetEffectiveAmplitude(OutputId), 5);
    }

    [Fact]
    public void GetEffectiveAmplitude_TwoBoostedSlidersMultiplyPastUnity()
    {
        var mixer = new AudioOutputMixer();
        mixer.SetMasterVolume(1.5);
        mixer.SetOutputVolume(OutputId, 1.5);

        Assert.Equal(2.25f, mixer.GetEffectiveAmplitude(OutputId), 5);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void GetEffectiveAmplitude_EitherMuteSilencesTheOutput(bool masterMuted, bool outputMuted)
    {
        var mixer = new AudioOutputMixer();
        mixer.SetMasterMuted(masterMuted);
        mixer.SetOutputMuted(OutputId, outputMuted);

        Assert.Equal(0f, mixer.GetEffectiveAmplitude(OutputId));
    }

    [Fact]
    public void SetOutputMuted_DoesNotDiscardTheOutputsVolume()
    {
        var mixer = new AudioOutputMixer();
        mixer.SetOutputVolume(OutputId, 0.5);

        mixer.SetOutputMuted(OutputId, true);
        mixer.SetOutputMuted(OutputId, false);

        Assert.Equal(0.5, mixer.Volumes[OutputId].Volume);
        Assert.Equal(0.25f, mixer.GetEffectiveAmplitude(OutputId), 5);
    }

    [Theory]
    [InlineData(-1, AudioVolumeLimits.Min)]
    [InlineData(99, AudioVolumeLimits.Max)]
    public void SetMasterVolume_ClampsToLimits(double requested, double expected)
    {
        var mixer = new AudioOutputMixer();

        mixer.SetMasterVolume(requested);

        Assert.Equal(expected, mixer.MasterVolume);
    }

    [Theory]
    [InlineData(-1, AudioVolumeLimits.Min)]
    [InlineData(99, AudioVolumeLimits.Max)]
    public void SetOutputVolume_ClampsToLimits(double requested, double expected)
    {
        var mixer = new AudioOutputMixer();

        mixer.SetOutputVolume(OutputId, requested);

        Assert.Equal(expected, mixer.Volumes[OutputId].Volume);
    }

    [Theory]
    [InlineData(-99, AudioDelayLimits.Min)]
    [InlineData(99, AudioDelayLimits.Max)]
    [InlineData(0.25, 0.25)]
    public void SetOutputDelay_ClampsToLimits(double requested, double expected)
    {
        var mixer = new AudioOutputMixer();

        mixer.SetOutputDelay(OutputId, requested);

        Assert.Equal(expected, mixer.GetDelaySeconds(OutputId));
    }

    [Fact]
    public void PerOutputStateIsIndependentAcrossOutputs()
    {
        var mixer = new AudioOutputMixer();

        mixer.SetOutputVolume(1, 0.5);
        mixer.SetOutputMuted(2, true);
        mixer.SetOutputDelay(3, 0.4);

        Assert.Equal(0.25f, mixer.GetEffectiveAmplitude(1), 5);
        Assert.Equal(0f, mixer.GetEffectiveAmplitude(2));
        Assert.Equal(1f, mixer.GetEffectiveAmplitude(3), 5);
        Assert.Equal(0.4, mixer.GetDelaySeconds(3));
        Assert.Equal(0, mixer.GetDelaySeconds(1));
    }
}
