using Avalonia.Layout;
using OMP.Ui.Extensions;

namespace OMP.Ui.Tests.Extensions;

public class AlignmentExtTests
{
    [Theory]
    [InlineData(HorizontalAlignment.Left)]
    [InlineData(HorizontalAlignment.Center)]
    [InlineData(HorizontalAlignment.Right)]
    [InlineData(HorizontalAlignment.Stretch)]
    public void ToDisplayLabel_HorizontalAlignment_ReturnsNonEmptyLabel(HorizontalAlignment value) =>
        Assert.False(string.IsNullOrEmpty(value.ToDisplayLabel()));

    [Fact]
    public void ToDisplayLabel_HorizontalLeftAndRight_AreDistinct() =>
        Assert.NotEqual(HorizontalAlignment.Left.ToDisplayLabel(), HorizontalAlignment.Right.ToDisplayLabel());

    [Theory]
    [InlineData(VerticalAlignment.Top)]
    [InlineData(VerticalAlignment.Center)]
    [InlineData(VerticalAlignment.Bottom)]
    [InlineData(VerticalAlignment.Stretch)]
    public void ToDisplayLabel_VerticalAlignment_ReturnsNonEmptyLabel(VerticalAlignment value) =>
        Assert.False(string.IsNullOrEmpty(value.ToDisplayLabel()));

    [Fact]
    public void ToDisplayLabel_VerticalTopAndBottom_AreDistinct() =>
        Assert.NotEqual(VerticalAlignment.Top.ToDisplayLabel(), VerticalAlignment.Bottom.ToDisplayLabel());
}
