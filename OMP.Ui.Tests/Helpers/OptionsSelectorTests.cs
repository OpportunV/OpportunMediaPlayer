using OMP.Ui.Helpers;

namespace OMP.Ui.Tests.Helpers;

public class OptionsSelectorTests
{
    [Fact]
    public void AvailableOptions_NoUsedKeys_ReturnsAllOptions()
    {
        var available = OptionsSelector.AvailableOptions([1, 2, 3], Enumerable.Empty<int>(), i => i);

        Assert.Equal([1, 2, 3], available);
    }

    [Fact]
    public void AvailableOptions_UsedKeyPresent_ExcludesMatchingOption()
    {
        var available = OptionsSelector.AvailableOptions([1, 2, 3], [2], i => i);

        Assert.Equal([1, 3], available);
    }

    [Fact]
    public void AvailableOptions_AllKeysUsed_ReturnsEmpty()
    {
        var available = OptionsSelector.AvailableOptions([1, 2], [1, 2], i => i);

        Assert.Empty(available);
    }

    [Fact]
    public void AvailableOptions_UsedKeyNotPresentInAll_IsIgnored()
    {
        var available = OptionsSelector.AvailableOptions(["a", "b"], ["z"], s => s);

        Assert.Equal(["a", "b"], available);
    }
}
