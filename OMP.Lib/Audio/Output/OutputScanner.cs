using NAudio.CoreAudioApi;

namespace OMP.Lib.Audio.Output;

public sealed class OutputScanner
{
    public IReadOnlyList<AudioOutput> ScanOutputs()
    {
        var enumerator = new MMDeviceEnumerator();
        return enumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select((device, i) => new AudioOutput(i, device.FriendlyName))
            .ToList();
    }
}