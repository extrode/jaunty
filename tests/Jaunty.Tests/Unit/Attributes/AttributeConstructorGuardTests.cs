using Jaunty.Attributes;

using Xunit;

namespace Jaunty.Tests.Unit;

/// <summary>
/// AUD-R35-137 and AUD-R35-138. <c>EnumStorageAttribute</c> stored whatever it was handed, and the
/// reflection and generated paths then disagreed about what an undefined value meant;
/// <c>ColumnAttribute</c>'s null guard - the only executable line in its file - had no test at all.
/// </summary>
public class AttributeConstructorGuardTests
{
    [Theory]
    [InlineData(EnumStorage.Numeric)]
    [InlineData(EnumStorage.String)]
    public void ADefinedStorageStrategy_IsKept(EnumStorage storage)
    {
        Assert.Equal(storage, new EnumStorageAttribute(storage).Storage);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(-1)]
    public void AnUndefinedStorageStrategy_IsRejectedAtTheSource(int raw)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new EnumStorageAttribute((EnumStorage)raw));

        Assert.Equal("storage", ex.ParamName);
        Assert.Equal((EnumStorage)raw, ex.ActualValue);
    }

    [Fact]
    public void AColumnNameIsRequired()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ColumnAttribute(null!));

        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void AColumnNameIsKeptVerbatim()
    {
        Assert.Equal("order_id", new ColumnAttribute("order_id").Name);
    }
}
