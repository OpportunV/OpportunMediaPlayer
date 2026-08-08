using System.Runtime.InteropServices;

namespace OMP.Lib.Interop;

internal static class WinMm
{
    public static IReadOnlyList<string> GetWaveOutProductNames()
    {
        var count = WaveOutGetNumDevs();
        var names = new List<string>(count);

        for (var deviceNumber = 0; deviceNumber < count; deviceNumber++)
        {
            names.Add(
                WaveOutGetDevCaps(deviceNumber, out var caps, Marshal.SizeOf<WaveOutCaps>()) == 0
                    ? caps.ProductName
                    : string.Empty);
        }

        return names;
    }

    [DllImport("winmm.dll", EntryPoint = "waveOutGetNumDevs")]
    private static extern int WaveOutGetNumDevs();

    [DllImport("winmm.dll", EntryPoint = "waveOutGetDevCapsW", CharSet = CharSet.Unicode)]
    private static extern int WaveOutGetDevCaps(nint deviceId, out WaveOutCaps caps, int capsSize);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WaveOutCaps
    {
        public short ManufacturerId;
        public short ProductId;
        public int DriverVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string ProductName;

        public int Formats;
        public short Channels;
        public short Reserved;
        public int Support;
    }
}
