using OMP.Lib.Audio.Output;

namespace OMP.Lib.Tests;

public class AlsaOutputDeviceFilterTests
{
    [Theory]
    [InlineData("HD-Audio Generic: ALC3234 Analog (hw:0,0)", true)]
    [InlineData("HDMI 0 (hw:1,3)", true)]
    [InlineData("surround40:CARD=PCH,DEV=0", false)]
    [InlineData("iec958:CARD=PCH,DEV=0", false)]
    [InlineData("dmix:CARD=PCH,DEV=0", false)]
    [InlineData("default", false)]
    [InlineData("pulse", false)]
    [InlineData("sysdefault", false)]
    public void IsRealHardwareDevice_MatchesOnHwSuffix(string deviceName, bool expected) =>
        Assert.Equal(expected, AlsaOutputDeviceFilter.IsRealHardwareDevice(deviceName));
}
