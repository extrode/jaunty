using Jaunty.Scaffolding.Providers.MySql;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class MySqlTypeMapperTests
{
    private readonly MySqlTypeMapper _mapper = new();

    private static ColumnSchema CreateColumn(
        string dataType, bool isNullable = false, int? maxLength = null, string? columnType = null) =>
        new()
        {
            ColumnName = "test_column",
            DataType = dataType,
            IsNullable = isNullable,
            OrdinalPosition = 1,
            MaxLength = maxLength,
            ColumnType = columnType
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

    [Theory]
    [InlineData("bit", "bit(1)")]
    [InlineData("tinyint", "tinyint(1)")]
    [InlineData("tinyint", "tinyint(1) unsigned")]
    public void MapToCSharpType_ColumnTypeDisplayWidthOne_ReturnsBool(string sqlType, string columnType)
    {
        // MySQL's tinyint(1)/bit(1) boolean convention is only observable via COLUMN_TYPE's
        // display width. INFORMATION_SCHEMA.COLUMNS.CHARACTER_MAXIMUM_LENGTH (ColumnSchema's
        // MaxLength) is always NULL for numeric columns, so it can never carry this signal.
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType, columnType: columnType));
        Assert.Equal("bool", result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Fact]
    public void MapToCSharpType_MaxLengthOneWithoutColumnType_DoesNotReturnBool()
    {
        // Regression guard: MaxLength alone must NOT trigger the bool convention, since a real
        // MySqlSchemaReader read never populates MaxLength for tinyint/bit columns (only
        // ColumnType carries the display-width signal).
        var result = _mapper.MapToCSharpType(CreateColumn("tinyint", maxLength: 1));
        Assert.Equal("sbyte", result.TypeName);
    }

    [Fact]
    public void MapToCSharpType_BitWithWiderColumnType_ReturnsUlong()
    {
        var result = _mapper.MapToCSharpType(CreateColumn("bit", columnType: "bit(8)"));
        Assert.Equal("ulong", result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Fact]
    public void MapToCSharpType_TinyintWithoutColumnType_ReturnsSbyte()
    {
        // No ColumnType at all must NOT be treated as the tinyint(1) boolean case.
        var result = _mapper.MapToCSharpType(CreateColumn("tinyint"));
        Assert.Equal("sbyte", result.TypeName);
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
    [InlineData("dec")]
    [InlineData("fixed")]
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
    // Spatial types
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("geometry")]
    [InlineData("point")]
    [InlineData("linestring")]
    [InlineData("polygon")]
    [InlineData("multipoint")]
    [InlineData("multilinestring")]
    [InlineData("multipolygon")]
    [InlineData("geometrycollection")]
    public void MapToCSharpType_SpatialTypes_ReturnsByteArray(string sqlType)
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
