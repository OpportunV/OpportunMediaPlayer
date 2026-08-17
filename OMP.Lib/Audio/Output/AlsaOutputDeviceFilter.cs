namespace OMP.Lib.Audio.Output;

internal static class AlsaOutputDeviceFilter
{
    private const string HardwareDeviceMarker = "(hw:";

    public static bool IsRealHardwareDevice(string deviceName) =>
        deviceName.Contains(HardwareDeviceMarker, StringComparison.Ordinal);
}
