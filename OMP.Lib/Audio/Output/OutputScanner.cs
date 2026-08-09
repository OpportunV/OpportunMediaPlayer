using Microsoft.Extensions.Logging;
using OMP.Lib.Interop;
using PortAudioSharp;

namespace OMP.Lib.Audio.Output;

internal sealed class OutputScanner(ILoggerFactory loggerFactory)
{
    public string? UnavailableReason { get; private set; }

    private readonly ILogger _logger = loggerFactory.CreateLogger<OutputScanner>();

    public IReadOnlyList<AudioOutput> ScanOutputs()
    {
        List<AudioOutput> outputs;

        try
        {
            PortAudioEnvironment.EnsureInitialized(_logger);

            var wasapiHostApiIndex = OperatingSystem.IsWindows()
                ? PortAudioHostApi.TryGetWasapiHostApiIndex(_logger)
                : null;

            outputs = [];
            for (var deviceIndex = 0; deviceIndex < PortAudio.DeviceCount; deviceIndex++)
            {
                var info = PortAudio.GetDeviceInfo(deviceIndex);
                if (info.maxOutputChannels <= 0)
                {
                    continue;
                }

                if (wasapiHostApiIndex is { } wasapiIndex && info.hostApi != wasapiIndex)
                {
                    continue;
                }

                outputs.Add(new AudioOutput(deviceIndex, info.name));
            }
        }
        catch (DllNotFoundException ex)
        {
            UnavailableReason = ex.Message;
            _logger.LogError(ex, "PortAudio native library failed to load.");
            return [];
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
