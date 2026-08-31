using System;
using Jaunty.Fluent.Internals;
using Xunit;

namespace Jaunty.Fluent.Tests.Unit;

public class CountConversionTests
{
    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    [InlineData((long)int.MaxValue)]
    public void ToInt32_WithinRange_ReturnsValue(long value)
    {
        Assert.Equal((int)value, CountConversion.ToInt32(value));
    }

    [Fact]
    public void ToInt32_AboveIntMax_ThrowsOverflowPointingAtLongCount()
    {
        var ex = Assert.Throws<OverflowException>(() => CountConversion.ToInt32((long)int.MaxValue + 1));
        Assert.Contains("LongCount", ex.Message);
    }
}
