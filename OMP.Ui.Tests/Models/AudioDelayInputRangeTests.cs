using OMP.Ui.Models;

namespace OMP.Ui.Tests.Models;

public class AudioDelayInputRangeTests
{
    [Fact]
    public void MinMs_IsMinusFiveThousand() => Assert.Equal(-5000, AudioDelayInputRange.MinMs);

    [Fact]
    public void MaxMs_IsFiveThousand() => Assert.Equal(5000, AudioDelayInputRange.MaxMs);
}
