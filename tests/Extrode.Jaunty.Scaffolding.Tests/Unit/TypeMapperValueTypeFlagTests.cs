using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Providers.MySql;
using Extrode.Jaunty.Scaffolding.Providers.PostgreSql;
using Extrode.Jaunty.Scaffolding.Providers.SQLite;
using Extrode.Jaunty.Scaffolding.Providers.SqlServer;
using Extrode.Jaunty.Scaffolding.Schema;

using Xunit;

namespace Extrode.Jaunty.Scaffolding.Tests.Unit;

/// <summary>
/// The full <c>CSharpTypeInfo</c> - name and value-type flag - for the type spellings whose flag or
/// alias had no assertion.
/// </summary>
public class TypeMapperValueTypeFlagTests
{
    private static (string, bool) Map(ITypeMapper mapper, string dataType, string? columnType = null)
    {
        CSharpTypeInfo info = mapper.MapToCSharpType(new ColumnSchema { ColumnName = "c", DataType = dataType, ColumnType = columnType });
        return (info.TypeName, info.IsValueType);
    }

    [Theory]
    [InlineData("date", "DateOnly", true)]
    [InlineData("time", "TimeSpan", true)]
    [InlineData("datetime", "DateTime", true)]
    [InlineData("timestamp", "DateTime", true)]
    [InlineData("year", "int", true)]
    [InlineData("no_such_type", "object", false)]
    public void MySql(string dataType, string typeName, bool isValueType)
        => Assert.Equal((typeName, isValueType), Map(new MySqlTypeMapper(), dataType));

    [Fact]
    public void MySql_AColumnTypeStartingWithUnsigned_IsUnsigned()
        => Assert.Equal(("uint", true), Map(new MySqlTypeMapper(), "int", "unsigned"));

    [Theory]
    [InlineData("date", "DateOnly", true)]
    [InlineData("time", "TimeOnly", true)]
    [InlineData("time without time zone", "TimeOnly", true)]
    [InlineData("timestamp", "DateTime", true)]
    [InlineData("timestamp without time zone", "DateTime", true)]
    [InlineData("timestamp with time zone", "DateTimeOffset", true)]
    [InlineData("timestamptz", "DateTimeOffset", true)]
    [InlineData("interval", "TimeSpan", true)]
    [InlineData("xml", "string", false)]
    [InlineData("cidr", "string", false)]
    [InlineData("tsvector", "object", false)]
    public void PostgreSql(string dataType, string typeName, bool isValueType)
        => Assert.Equal((typeName, isValueType), Map(new PostgreSqlTypeMapper(), dataType));

    [Theory]
    [InlineData("CHARACTER(20)", "string", false)]
    [InlineData("VARYING CHARACTER(255)", "string", false)]
    [InlineData("NCHAR(55)", "string", false)]
    [InlineData("NATIVE CHARACTER(70)", "string", false)]
    [InlineData("CLOB", "string", false)]
    [InlineData("(10)", "object", false)]
    public void Sqlite(string dataType, string typeName, bool isValueType)
        => Assert.Equal((typeName, isValueType), Map(new SQLiteTypeMapper(), dataType));

    [Fact]
    public void SqlServer_SqlVariant_IsObject()
        => Assert.Equal(("object", false), Map(new SqlServerTypeMapper(), "sql_variant"));
}
