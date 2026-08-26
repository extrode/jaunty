using Jaunty.Core;
using Jaunty.Internals;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R35-122. The <c>ExpectedRowCount</c> hint used to go straight into
/// <c>new List&lt;T&gt;(capacity)</c>, so a bad value aborted the query it was meant to speed up.
/// </summary>
public class CapacityHintTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ANonPositiveOrAbsentHint_ReadsAsNoHint(int? given)
    {
        Assert.Null(CapacityHint.Normalize(given));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(CapacityHint.MaxExpectedRowCount)]
    public void AUsableHint_IsKept(int given)
    {
        Assert.Equal(given, CapacityHint.Normalize(given));
    }

    [Theory]
    [InlineData(CapacityHint.MaxExpectedRowCount + 1)]
    [InlineData(int.MaxValue)]
    public void AnAbsurdHint_IsCappedRatherThanAllocated(int given)
    {
        Assert.Equal(CapacityHint.MaxExpectedRowCount, CapacityHint.Normalize(given));
    }

    [Fact]
    public void WithExpectedRowCount_NormalisesOnTheWayIn()
    {
        Assert.Null(CommandOptions<object>.WithExpectedRowCount(-1).ExpectedRowCount);
        Assert.Null(CommandOptions<object>.WithExpectedRowCount(0).ExpectedRowCount);
        Assert.Equal(500, CommandOptions<object>.WithExpectedRowCount(500).ExpectedRowCount);
        Assert.Equal(
            CapacityHint.MaxExpectedRowCount,
            CommandOptions<object>.WithExpectedRowCount(int.MaxValue).ExpectedRowCount);
    }

    [Fact]
    public void TheMultiEntityOptions_NormaliseTheSameWay()
    {
        var negative = new MultiEntityCommandOptions<object, object>(expectedRowCount: -1);
        var absurd = new MultiEntityCommandOptions<object, object>(expectedRowCount: int.MaxValue);

        Assert.Null(negative.ExpectedRowCount);
        Assert.Equal(CapacityHint.MaxExpectedRowCount, absurd.ExpectedRowCount);
    }
}
