using Jaunty.Scaffolding.Providers.MySql;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class MySqlTypeMapperTests
{
    private readonly MySqlTypeMapper _mapper = new();

    private static ColumnSchema CreateColumn(string dataType, bool isNullable = false) =>
        new()
        {
            ColumnName = "test_column",
            DataType = dataType,
            IsNullable = isNullable,
            OrdinalPosition = 1
        };

    // ------------------------------------------------------------------
    // Boolean
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("boolean")]
    [InlineData("bool")]
    public void MapToCSharpType_BooleanTypes_ReturnsBool(string sqlType)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal("bool", result.TypeName);
        Assert.True(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // Integer types
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("tinyint", "sbyte")]
    [InlineData("smallint", "short")]
    [InlineData("mediumint", "int")]
    [InlineData("int", "int")]
    [InlineData("integer", "int")]
    [InlineData("bigint", "long")]
    public void MapToCSharpType_IntegerTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal(expected, result.TypeName);
        Assert.True(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // Float types
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("float", "float")]
    [InlineData("double", "double")]
    [InlineData("real", "double")]
    public void MapToCSharpType_FloatTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal(expected, result.TypeName);
        Assert.True(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // Decimal types
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("decimal")]
    [InlineData("numeric")]
    public void MapToCSharpType_DecimalTypes_ReturnsDecimal(string sqlType)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal("decimal", result.TypeName);
        Assert.True(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // String types
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("char")]
    [InlineData("varchar")]
    [InlineData("text")]
    [InlineData("tinytext")]
    [InlineData("mediumtext")]
    [InlineData("longtext")]
    [InlineData("enum")]
    [InlineData("set")]
    public void MapToCSharpType_TextTypes_ReturnsString(string sqlType)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal("string", result.TypeName);
        Assert.False(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // Date/Time types
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("date", "DateOnly")]
    [InlineData("time", "TimeOnly")]
    [InlineData("datetime", "DateTime")]
    [InlineData("timestamp", "DateTime")]
    [InlineData("year", "short")]
    public void MapToCSharpType_DateTimeTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal(expected, result.TypeName);
    }

    // ------------------------------------------------------------------
    // Binary types
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("binary")]
    [InlineData("varbinary")]
    [InlineData("blob")]
    [InlineData("tinyblob")]
    [InlineData("mediumblob")]
    [InlineData("longblob")]
    public void MapToCSharpType_BinaryTypes_ReturnsByteArray(string sqlType)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal("byte[]", result.TypeName);
        Assert.False(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------

    [Fact]
    public void MapToCSharpType_Json_ReturnsString()
    {
        var result = _mapper.MapToCSharpType(CreateColumn("json"));
        Assert.Equal("string", result.TypeName);
        Assert.False(result.IsValueType);
    }

        // ------------------------------------------------------------------
    // Unknown type fallback
    // ------------------------------------------------------------------

    [Fact]
    public void MapToCSharpType_UnknownType_ReturnsObject()
    {
        var result = _mapper.MapToCSharpType(CreateColumn("unknown_custom_type_xyz"));
        Assert.Equal("object", result.TypeName);
    }
}
