using Jaunty.Scaffolding.Providers.SQLite;
using Jaunty.Scaffolding.Schema;
using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class SQLiteTypeMapperTests
{
    private readonly SQLiteTypeMapper _mapper = new();

    private static ColumnSchema CreateColumn(string dataType, bool isNullable = false) =>
        new()
        {
            ColumnName = "test_column",
            DataType = dataType,
            IsNullable = isNullable,
            OrdinalPosition = 1
        };

    [Theory]
    [InlineData("INTEGER", "long")]
    [InlineData("INT", "long")]
    [InlineData("TINYINT", "long")]
    [InlineData("SMALLINT", "long")]
    [InlineData("MEDIUMINT", "long")]
    [InlineData("BIGINT", "long")]
    [InlineData("INT2", "long")]
    [InlineData("INT8", "long")]
    public void MapToCSharpType_IntegerTypes_ReturnsLong(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("REAL", "double")]
    [InlineData("DOUBLE", "double")]
    [InlineData("DOUBLE PRECISION", "double")]
    [InlineData("FLOAT", "double")]
    public void MapToCSharpType_FloatTypes_ReturnsDouble(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("NUMERIC", "decimal")]
    [InlineData("DECIMAL", "decimal")]
    public void MapToCSharpType_DecimalTypes_ReturnsDecimal(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("TEXT", "string")]
    [InlineData("CHAR", "string")]
    [InlineData("VARCHAR", "string")]
    [InlineData("NCHAR", "string")]
    [InlineData("NVARCHAR", "string")]
    [InlineData("CLOB", "string")]
    public void MapToCSharpType_TextTypes_ReturnsString(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.False(result.IsValueType);
    }

    [Theory]
    [InlineData("BLOB", "byte[]")]
    [InlineData("NONE", "byte[]")]
    public void MapToCSharpType_BlobTypes_ReturnsByteArray(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.False(result.IsValueType);
    }

    [Theory]
    [InlineData("BOOLEAN", "bool")]
    [InlineData("BOOL", "bool")]
    public void MapToCSharpType_BooleanTypes_ReturnsBool(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("DATE", "DateOnly")]
    [InlineData("TIME", "TimeOnly")]
    [InlineData("DATETIME", "DateTime")]
    [InlineData("TIMESTAMP", "DateTime")]
    public void MapToCSharpType_DateTimeTypes_ReturnsCorrectType(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("GUID", "Guid")]
    [InlineData("UUID", "Guid")]
    [InlineData("UNIQUEIDENTIFIER", "Guid")]
    public void MapToCSharpType_GuidTypes_ReturnsGuid(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Fact]
    public void MapToCSharpType_UnknownType_ReturnsObject()
    {
        var column = CreateColumn("UNKNOWN_TYPE");
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal("object", result.TypeName);
        Assert.False(result.IsValueType);
    }

    [Fact]
    public void MapToCSharpType_TypeWithSize_HandlesParentheses()
    {
        var column = CreateColumn("VARCHAR(255)");
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal("string", result.TypeName);
    }

    [Fact]
    public void MapToCSharpType_LowercaseType_HandlesCorrectly()
    {
        var column = CreateColumn("integer");
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal("long", result.TypeName);
    }
}
