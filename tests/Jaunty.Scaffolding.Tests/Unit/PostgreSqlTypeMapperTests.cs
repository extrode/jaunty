using Jaunty.Scaffolding.Providers.PostgreSql;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class PostgreSqlTypeMapperTests
{
    private readonly PostgreSqlTypeMapper _mapper = new();

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
    [InlineData("smallint", "short")]
    [InlineData("int2", "short")]
    [InlineData("integer", "int")]
    [InlineData("int", "int")]
    [InlineData("int4", "int")]
    [InlineData("bigint", "long")]
    [InlineData("int8", "long")]
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
    [InlineData("real", "float")]
    [InlineData("float4", "float")]
    [InlineData("double precision", "double")]
    [InlineData("float8", "double")]
    public void MapToCSharpType_FloatTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal(expected, result.TypeName);
        Assert.True(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // Decimal / Money
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("numeric")]
    [InlineData("decimal")]
    [InlineData("money")]
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
    [InlineData("character")]
    [InlineData("varchar")]
    [InlineData("character varying")]
    [InlineData("text")]
    [InlineData("name")]
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
    [InlineData("time without time zone", "TimeOnly")]
    [InlineData("timestamp", "DateTime")]
    [InlineData("timestamp without time zone", "DateTime")]
    [InlineData("timestamp with time zone", "DateTimeOffset")]
    [InlineData("timestamptz", "DateTimeOffset")]
    [InlineData("interval", "TimeSpan")]
    public void MapToCSharpType_DateTimeTypes_MapsCorrectly(string sqlType, string expected)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal(expected, result.TypeName);
    }

    // ------------------------------------------------------------------
    // UUID
    // ------------------------------------------------------------------

    [Fact]
    public void MapToCSharpType_Uuid_ReturnsGuid()
    {
        var result = _mapper.MapToCSharpType(CreateColumn("uuid"));
        Assert.Equal("Guid", result.TypeName);
        Assert.True(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // Binary
    // ------------------------------------------------------------------

    [Fact]
    public void MapToCSharpType_Bytea_ReturnsByteArray()
    {
        var result = _mapper.MapToCSharpType(CreateColumn("bytea"));
        Assert.Equal("byte[]", result.TypeName);
        Assert.False(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("json")]
    [InlineData("jsonb")]
    public void MapToCSharpType_JsonTypes_ReturnsString(string sqlType)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal("string", result.TypeName);
        Assert.False(result.IsValueType);
    }

    // ------------------------------------------------------------------
    // Network / other types that map to string
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("inet")]
    [InlineData("cidr")]
    [InlineData("macaddr")]
    [InlineData("xml")]
    public void MapToCSharpType_NetworkAndXmlTypes_ReturnsString(string sqlType)
    {
        var result = _mapper.MapToCSharpType(CreateColumn(sqlType));
        Assert.Equal("string", result.TypeName);
    }

    // ------------------------------------------------------------------
    // OID
    // ------------------------------------------------------------------

    [Fact]
    public void MapToCSharpType_Oid_ReturnsUint()
    {
        var result = _mapper.MapToCSharpType(CreateColumn("oid"));
        Assert.Equal("uint", result.TypeName);
        Assert.True(result.IsValueType);
    }

        // ------------------------------------------------------------------
    // Unknown type fallback
    // ------------------------------------------------------------------

    [Fact]
    public void MapToCSharpType_UnknownType_ReturnsObject()
    {
        var result = _mapper.MapToCSharpType(CreateColumn("user_defined_type_xyz"));
        Assert.Equal("object", result.TypeName);
    }
}
