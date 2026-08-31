using Jaunty.Internals.Read;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-126. The <c>DBNull</c> guard sat below the <c>value is T direct</c> fast path, so it did
/// not hold for the one <c>T</c> that can match <c>DBNull.Value</c> directly. Neither current caller
/// can reach it - both filter <c>null or DBNull</c> before calling in - so these pin the converter's
/// standalone contract rather than a live path.
/// </summary>
public class ScalarConverterDbNullTests
{
    [Fact]
    public void ConvertingDbNullToObject_ReturnsNull_NotDbNull()
    {
        Assert.Null(ScalarConverter<object>.Convert(DBNull.Value));
    }

    [Fact]
    public void ConvertingDbNullToDbNull_ReturnsNull()
    {
        Assert.Null(ScalarConverter<DBNull>.Convert(DBNull.Value));
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    public void ConvertingDbNullToAnyOtherType_StillReturnsDefault(Type target)
    {
        object? result = target == typeof(int)
            ? ScalarConverter<int>.Convert(DBNull.Value)
            : ScalarConverter<string>.Convert(DBNull.Value);

        if (target == typeof(int))
            Assert.Equal(0, result);
        else
            Assert.Null(result);
    }

    /// <summary>
    /// The control: reordering the guard must not cost the fast path, which is the reason the
    /// converter is shaped this way at all.
    /// </summary>
    [Fact]
    public void ANonNullValueOfTheTargetType_StillTakesTheFastPath()
    {
        object boxed = 42;

        Assert.Equal(42, ScalarConverter<int>.Convert(boxed));
        Assert.Same(boxed, ScalarConverter<object>.Convert(boxed));
        Assert.Equal("x", ScalarConverter<string>.Convert("x"));
    }
}
