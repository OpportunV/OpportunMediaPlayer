using Microsoft.Extensions.Logging;
using OMP.Lib.Interop;

namespace OMP.Lib.Audio.Output;

internal static class WaveOutDeviceResolver
{
    private const int WaveMapperDeviceNumber = -1;

    public static int Resolve(string friendlyName, ILogger logger)
    {
        var productNames = WinMm.GetWaveOutProductNames();

        for (var deviceNumber = 0; deviceNumber < productNames.Count; deviceNumber++)
        {
            var productName = productNames[deviceNumber];

            if (productName.Length > 0 && friendlyName.StartsWith(productName, StringComparison.OrdinalIgnoreCase))
            {
                return deviceNumber;
            }
        }

        logger.LogWarning(
            "No WinMM output device matches '{FriendlyName}'; falling back to the system default.",
            friendlyName);

        return WaveMapperDeviceNumber;
    }
}
