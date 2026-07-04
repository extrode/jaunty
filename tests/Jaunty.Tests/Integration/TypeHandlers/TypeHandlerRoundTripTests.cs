using System.Data;
using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.TypeHandlers;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.TypeHandlers;

[Collection("Type Handler Operations")]
public class TypeHandlerRoundTripTests : IClassFixture<DialectFixture>, IDisposable
{
    private readonly DialectFixture _fixture;

    public TypeHandlerRoundTripTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    public void Dispose()
    {
        // Only remove the specific handlers and state this class may have registered.
        // Do NOT call JauntyConfig.Reset() -- it wipes ReflectionMapperResolver,
        // causing cross-test mapper failures when running in parallel.
        JauntyConfig.RemoveTypeHandler<string>();
        JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;
    }

    private static void CreateTableForTest(IDbConnection connection, DialectProvider provider, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = provider switch
        {
            DialectProvider.SqlServer => CreateSqlServerTable(tableName),
            DialectProvider.Postgres => CreatePostgresTable(tableName),
            DialectProvider.MariaDb => CreateMariaDbTable(tableName),
            _ => CreateSqliteTable(tableName)
        };
        cmd.ExecuteNonQuery();
    }

    private static string CreateSqliteTable(string tableName)
    {
        return $@"CREATE TABLE IF NOT EXISTS {tableName} (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL,
            custom_value TEXT NULL,
            enum_string_override TEXT NULL,
            enum_global_string TEXT NULL
        )";
    }

    private static string CreateSqlServerTable(string tableName)
    {
        return $@"IF OBJECT_ID('dbo.{tableName}', 'U') IS NULL
        CREATE TABLE dbo.{tableName} (
            id BIGINT IDENTITY(1,1) PRIMARY KEY,
            name NVARCHAR(255) NOT NULL,
            custom_value NVARCHAR(MAX) NULL,
            enum_string_override NVARCHAR(MAX) NULL,
            enum_global_string NVARCHAR(MAX) NULL
        )";
    }

    private static string CreatePostgresTable(string tableName)
    {
        return $@"CREATE TABLE IF NOT EXISTS {tableName} (
            id BIGSERIAL PRIMARY KEY,
            name TEXT NOT NULL,
            custom_value TEXT NULL,
            enum_string_override TEXT NULL,
            enum_global_string TEXT NULL
        )";
    }

    private static string CreateMariaDbTable(string tableName)
    {
        return $@"CREATE TABLE IF NOT EXISTS {tableName} (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            name VARCHAR(255) NOT NULL,
            custom_value LONGTEXT NULL,
            enum_string_override LONGTEXT NULL,
            enum_global_string LONGTEXT NULL
        )";
    }

    [Theory]
    [SystemSqlite]
    public void EnumAttribute_OverridesDefaultToString_RoundTrips(DialectInfo dialect)
    {
        string tableName = "typehandler_test_enum_attr";
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, tableName);

        var entity = new TypeHandlerTestEntity
        {
            Name = "Enum String Override",
            EnumStringOverride = TestEnumForHandlers.Completed
        };

        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tableName} (name, enum_string_override) VALUES (@name, @enum)";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = entity.Name; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName = "@enum"; p2.Value = entity.EnumStringOverride.ToString(); cmd.Parameters.Add(p2);
        cmd.ExecuteNonQuery();

        using var getCmd = ctx.Connection.CreateCommand();
        getCmd.CommandText = $"SELECT enum_string_override FROM {tableName} WHERE name = @name";
        var p3 = getCmd.CreateParameter(); p3.ParameterName = "@name"; p3.Value = entity.Name; getCmd.Parameters.Add(p3);
        var rawValue = getCmd.ExecuteScalar();
        Assert.NotNull(rawValue);
        Assert.Equal("Completed", rawValue.ToString());
    }

    [Theory]
    [SystemSqlite]
    public void GlobalEnumStorage_String_RoundTrips(DialectInfo dialect)
    {
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        string tableName = "typehandler_test_global_enum";
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, tableName);

        var entity = new TypeHandlerTestEntity
        {
            Name = "Enum Global String",
            EnumGlobalString = TestEnumForHandlers.Cancelled
        };

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tableName} (name, enum_global_string) VALUES (@name, @enum)";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = entity.Name; cmd.Parameters.Add(p1);
        var p2 = cmd.CreateParameter(); p2.ParameterName = "@enum"; p2.Value = entity.EnumGlobalString.ToString(); cmd.Parameters.Add(p2);
        cmd.ExecuteNonQuery();

        using var getCmd = ctx.Connection.CreateCommand();
        getCmd.CommandText = $"SELECT enum_global_string FROM {tableName} WHERE name = @name";
        var p3 = getCmd.CreateParameter(); p3.ParameterName = "@name"; p3.Value = entity.Name; getCmd.Parameters.Add(p3);
        var rawValue = getCmd.ExecuteScalar();
        Assert.NotNull(rawValue);
        Assert.Equal("Cancelled", rawValue.ToString());
    }

    [Theory]
    [SystemSqlite]
    public void DelegateBasedTypeHandler_StringDelegateRoundTrip_HandlerInvoked(DialectInfo dialect)
    {
        JauntyConfig.RegisterTypeHandler<string>(
            fromDb: dbValue =>
            {
                if (dbValue is null || dbValue == DBNull.Value)
                    return string.Empty;
                string str = dbValue.ToString()!;
                return "HANDLED:" + str;
            },
            toDb: value =>
            {
                if (value is null)
                    return null;
                if (value.StartsWith("HANDLED:"))
                    return value.Substring("HANDLED:".Length);
                return value;
            }
        );

        string tableName = "typehandler_test_delegate";
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, tableName);

        var entity = new TypeHandlerTestEntity
        {
            Name = "TestValue"
        };

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tableName} (name) VALUES (@name)";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = entity.Name; cmd.Parameters.Add(p1);
        cmd.ExecuteNonQuery();

        using var getCmd = ctx.Connection.CreateCommand();
        getCmd.CommandText = $"SELECT name FROM {tableName} WHERE name LIKE @prefix";
        var p2 = getCmd.CreateParameter(); p2.ParameterName = "@prefix"; p2.Value = "%TestValue%"; getCmd.Parameters.Add(p2);
        var rawValue = getCmd.ExecuteScalar();
        Assert.NotNull(rawValue);
        Assert.Equal("TestValue", rawValue.ToString());
    }

    [Theory]
    [SystemSqlite]
    public void ClassBasedTypeHandler_SubclassRoundTrip_HandlerInvoked(DialectInfo dialect)
    {
        var handler = new PrefixTypeHandler();
        JauntyConfig.RegisterTypeHandler<string>(handler);

        string tableName = "typehandler_test_class";
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, tableName);

        var entity = new TypeHandlerTestEntity
        {
            Name = "ClassHandlerTest"
        };

        using var cmd = ctx.Connection.CreateCommand();
        cmd.CommandText = $"INSERT INTO {tableName} (name) VALUES (@name)";
        var p1 = cmd.CreateParameter(); p1.ParameterName = "@name"; p1.Value = entity.Name; cmd.Parameters.Add(p1);
        cmd.ExecuteNonQuery();

        using var getCmd = ctx.Connection.CreateCommand();
        getCmd.CommandText = $"SELECT name FROM {tableName} WHERE name LIKE @prefix";
        var p2 = getCmd.CreateParameter(); p2.ParameterName = "@prefix"; p2.Value = "%ClassHandlerTest%"; getCmd.Parameters.Add(p2);
        var rawValue = getCmd.ExecuteScalar();
        Assert.NotNull(rawValue);
        Assert.Equal("ClassHandlerTest", rawValue.ToString());
    }

    private class PrefixTypeHandler : TypeHandler<string>
    {
        private const string Prefix = "CLASSHANDLED:";

        public override string Parse(object? dbValue)
        {
            if (dbValue is null || dbValue == DBNull.Value)
                return string.Empty;
            string str = dbValue.ToString()!;
            return Prefix + str;
        }

        public override object? ToDbValue(string? value)
        {
            if (value is null)
                return null;
            if (value.StartsWith(Prefix))
                return value.Substring(Prefix.Length);
            return value;
        }
    }

}