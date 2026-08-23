using OMP.Ui.Input;

namespace OMP.Ui.Tests.Input;

public class MainWindowHotkeysTests
{
    [Fact]
    public void All_HasNoDuplicateKeyModifierCombinations()
    {
        var duplicates = MainWindowHotkeys.All
            .GroupBy(binding => (binding.Key, binding.Modifiers))
            .Where(group => group.Count() > 1);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void All_HasNoDuplicateDescriptions()
    {
        var duplicates = MainWindowHotkeys.All
            .GroupBy(binding => binding.Description)
            .Where(group => group.Count() > 1);

        Assert.Empty(duplicates);
    }

    [Fact]
    public void All_EveryBindingHasNonEmptyDescription() =>
        Assert.All(MainWindowHotkeys.All, binding => Assert.False(string.IsNullOrWhiteSpace(binding.Description)));
}
