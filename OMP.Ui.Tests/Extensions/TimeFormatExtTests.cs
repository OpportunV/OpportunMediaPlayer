using OMP.Ui.Extensions;

namespace OMP.Ui.Tests.Extensions;

public class TimeFormatExtTests
{
    [Fact]
    public void Format_UnderOneHour_UsesMinutesSeconds() =>
        Assert.Equal("05:09", TimeSpan.FromSeconds(309).Format());

    [Fact]
    public void Format_OneHourOrMore_UsesHoursMinutesSeconds() =>
        Assert.Equal("01:00:00", TimeSpan.FromHours(1).Format());

    [Fact]
    public void Format_JustUnderOneHour_UsesMinutesSeconds() =>
        Assert.Equal("59:59", TimeSpan.FromSeconds(3599).Format());
}
