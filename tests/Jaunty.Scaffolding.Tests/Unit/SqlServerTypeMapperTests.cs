using Jaunty.Scaffolding.Providers.SqlServer;
using Jaunty.Scaffolding.Schema;

using Xunit;

namespace Jaunty.Scaffolding.Tests.Unit;

public class SqlServerTypeMapperTests
{
    private readonly SqlServerTypeMapper _mapper = new();

    private static ColumnSchema CreateColumn(string dataType, bool isNullable = false) =>
        new()
        {
            ColumnName = "test_column",
            DataType = dataType,
            IsNullable = isNullable,
            OrdinalPosition = 1
        };

    [Theory]
    [InlineData("bit", "bool")]
    [InlineData("BIT", "bool")]
    public void MapToCSharpType_BitType_ReturnsBool(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("tinyint", "byte")]
    [InlineData("smallint", "short")]
    [InlineData("int", "int")]
    [InlineData("bigint", "long")]
    public void MapToCSharpType_IntegerTypes_ReturnsCorrectType(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("real", "float")]
    [InlineData("float", "double")]
    public void MapToCSharpType_FloatTypes_ReturnsCorrectType(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("decimal", "decimal")]
    [InlineData("numeric", "decimal")]
    [InlineData("money", "decimal")]
    [InlineData("smallmoney", "decimal")]
    public void MapToCSharpType_DecimalTypes_ReturnsDecimal(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("char", "string")]
    [InlineData("varchar", "string")]
    [InlineData("text", "string")]
    [InlineData("nchar", "string")]
    [InlineData("nvarchar", "string")]
    [InlineData("ntext", "string")]
    [InlineData("xml", "string")]
    // AUD-R35-042: sysname is nvarchar(128) NOT NULL, and the catch-all answered "object".
    [InlineData("sysname", "string")]
    public void MapToCSharpType_StringTypes_ReturnsString(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.False(result.IsValueType);
    }

    [Theory]
    [InlineData("date", "DateOnly")]
    [InlineData("time", "TimeOnly")]
    [InlineData("datetime", "DateTime")]
    [InlineData("datetime2", "DateTime")]
    [InlineData("smalldatetime", "DateTime")]
    [InlineData("datetimeoffset", "DateTimeOffset")]
    public void MapToCSharpType_DateTimeTypes_ReturnsCorrectType(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Fact]
    public void MapToCSharpType_UniqueIdentifier_ReturnsGuid()
    {
        var column = CreateColumn("uniqueidentifier");
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal("Guid", result.TypeName);
        Assert.True(result.IsValueType);
    }

    [Theory]
    [InlineData("binary", "byte[]")]
    [InlineData("varbinary", "byte[]")]
    [InlineData("image", "byte[]")]
    [InlineData("rowversion", "byte[]")]
    [InlineData("timestamp", "byte[]")]
    public void MapToCSharpType_BinaryTypes_ReturnsByteArray(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.False(result.IsValueType);
    }

    [Theory]
    [InlineData("geography", "byte[]")]
    [InlineData("geometry", "byte[]")]
    [InlineData("hierarchyid", "byte[]")]
    public void MapToCSharpType_SpatialTypes_ReturnsByteArray(string sqlType, string expectedCSharpType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal(expectedCSharpType, result.TypeName);
        Assert.False(result.IsValueType);
    }

    [Fact]
    public void MapToCSharpType_SqlVariant_ReturnsObject()
    {
        var column = CreateColumn("sql_variant");
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal("object", result.TypeName);
        Assert.False(result.IsValueType);
    }

    [Fact]
    public void MapToCSharpType_UnknownType_ReturnsObject()
    {
        var column = CreateColumn("unknown_type");
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal("object", result.TypeName);
        Assert.False(result.IsValueType);
    }

    // AUD-R18 batch-8: RequiredUsing exists so EntityCodeGenerator.AppendUsings can emit
    // "using System;" for types like Guid/DateOnly that aren't in scope without ImplicitUsings -
    // no mapper populated it, so generated code failed to compile with ImplicitUsings disabled.
    [Theory]
    [InlineData("date")]
    [InlineData("time")]
    [InlineData("datetime")]
    [InlineData("datetime2")]
    [InlineData("smalldatetime")]
    [InlineData("datetimeoffset")]
    [InlineData("uniqueidentifier")]
    public void MapToCSharpType_SystemNamespaceTypes_SetsRequiredUsing(string sqlType)
    {
        var column = CreateColumn(sqlType);
        var result = _mapper.MapToCSharpType(column);
        Assert.Equal("System", result.RequiredUsing);
    }
}