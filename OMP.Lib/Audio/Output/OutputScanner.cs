using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;

namespace OMP.Lib.Audio.Output;

internal sealed class OutputScanner(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<OutputScanner>();

    public IReadOnlyList<AudioOutput> ScanOutputs()
    {
        List<AudioOutput> outputs;

        try
        {
            var enumerator = new MMDeviceEnumerator();
            outputs = enumerator
                .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Select((device, i) => new AudioOutput(i, device.FriendlyName))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate audio render endpoints.");
            return [];
        }

        foreach (var output in outputs)
        {
            _logger.LogDebug("Audio output {OutputId}: '{FriendlyName}'.", output.Id, output.FriendlyName);
        }

        if (outputs.Count == 0)
        {
            _logger.LogWarning("No active audio render endpoints found.");
        }
        else
        {
            _logger.LogInformation("Found {Count} audio output(s).", outputs.Count);
        }

        return outputs;
    }
}
