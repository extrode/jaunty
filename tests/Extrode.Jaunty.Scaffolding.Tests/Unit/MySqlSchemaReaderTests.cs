using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Providers.MySql;
using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

public class MySqlSchemaReaderTests
{
    [Fact]
    public void ShouldSkipDatabase_NoIncludeSchemas_ReturnsFalse()
    {
        var options = new SchemaReaderOptions();

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_IncludeSchemasContainsCurrentDatabase_ReturnsFalse()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = ["mydb"] };

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_IncludeSchemasContainsCurrentDatabase_CaseInsensitive_ReturnsFalse()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = ["MyDb"] };

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_IncludeSchemasExcludesCurrentDatabase_ReturnsTrue()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = ["otherdb"] };

        Assert.True(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_IncludeSchemasHasMultipleDatabasesIncludingCurrent_ReturnsFalse()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = ["otherdb", "mydb"] };

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Fact]
    public void ShouldSkipDatabase_EmptyIncludeSchemas_ReturnsFalse()
    {
        var options = new SchemaReaderOptions { IncludeSchemas = [] };

        Assert.False(MySqlSchemaReader.ShouldSkipDatabase(options, "mydb"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(42, 42)]
    [InlineData(-5, -5)]
    [InlineData(2147483647, int.MaxValue)]
    [InlineData(2147483648L, int.MaxValue)]
    [InlineData(4294967295L, int.MaxValue)]
    public void ToClampedInt32_IntegralValues_ClampToInt32(long value, int expected)
    {
        Assert.Equal(expected, MySqlSchemaReader.ToClampedInt32(value));
    }

    [Theory]
    [InlineData("18446744073709551615", int.MaxValue)]
    [InlineData("7", 7)]
    public void ToClampedInt32_UnsignedAndDecimalValues_ClampToInt32(string value, int expected)
    {
        Assert.Equal(expected, MySqlSchemaReader.ToClampedInt32(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(expected, MySqlSchemaReader.ToClampedInt32(ulong.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
