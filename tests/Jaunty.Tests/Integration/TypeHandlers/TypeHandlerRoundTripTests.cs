using System.Data;
using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.StoredProcedure;
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

    // Must match TypeHandlerTestEntity's [Table(...)] mapping - Insert<T>/Query<T> resolve
    // the table name from entity metadata, not from a string passed at the call site.
    private const string TableName = "typehandler_test";

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
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        Assert.Equal(EnumStorage.Numeric, JauntyConfig.DefaultEnumStorage);

        var entity = new TypeHandlerTestEntity
        {
            Name = "Enum String Override",
            EnumStringOverride = TestEnumForHandlers.Completed
        };

        // Goes through Jaunty's real write pipeline (WriteParameterCache / TypeHandler
        // resolution), not raw ADO.NET - exercises the same code path production callers use.
        ctx.Connection.Insert(entity);

        // [EnumStorage(EnumStorage.String)] on EnumStringOverride overrides the process-wide
        // Numeric default. If the attribute weren't honored by the read pipeline, this would
        // either fail to parse the stored string back into the enum or never have been stored
        // as a string in the first place.
        var roundTripped = ctx.Connection.QueryFirst<TypeHandlerTestEntity>(
            $"SELECT * FROM {TableName} WHERE name = @Name", new { entity.Name });
        Assert.Equal(TestEnumForHandlers.Completed, roundTripped.EnumStringOverride);

        var raw = ctx.Connection.QueryScalar<string>(
            $"SELECT enum_string_override FROM {TableName} WHERE name = @Name", new { entity.Name });
        Assert.Equal("Completed", raw);
    }

    [Theory]
    [SystemSqlite]
    public void GlobalEnumStorage_String_RoundTrips(DialectInfo dialect)
    {
        JauntyConfig.DefaultEnumStorage = EnumStorage.String;

        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entity = new TypeHandlerTestEntity
        {
            Name = "Enum Global String",
            EnumGlobalString = TestEnumForHandlers.Cancelled
        };

        ctx.Connection.Insert(entity);

        var roundTripped = ctx.Connection.QueryFirst<TypeHandlerTestEntity>(
            $"SELECT * FROM {TableName} WHERE name = @Name", new { entity.Name });
        Assert.Equal(TestEnumForHandlers.Cancelled, roundTripped.EnumGlobalString);

        var raw = ctx.Connection.QueryScalar<string>(
            $"SELECT enum_global_string FROM {TableName} WHERE name = @Name", new { entity.Name });
        Assert.Equal("Cancelled", raw);
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

        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entity = new TypeHandlerTestEntity { Name = "TestValue" };

        // toDb is a passthrough for values without the "HANDLED:" prefix, so this stores
        // "TestValue" unchanged - if it weren't invoked at all this assertion wouldn't
        // distinguish that, which is exactly why the read-back check below matters.
        ctx.Connection.Insert(entity);

        // fromDb prefixes "HANDLED:" onto whatever is read. This only comes back prefixed
        // if Jaunty's QueryFirst<T> actually invoked the registered delegate-based handler
        // while mapping the row - the prior version of this test never asserted this.
        var roundTripped = ctx.Connection.QueryFirst<TypeHandlerTestEntity>(
            $"SELECT * FROM {TableName} WHERE name = @Name", new { entity.Name });
        Assert.Equal("HANDLED:TestValue", roundTripped.Name);
    }

    [Theory]
    [SystemSqlite]
    public void ClassBasedTypeHandler_SubclassRoundTrip_HandlerInvoked(DialectInfo dialect)
    {
        var handler = new PrefixTypeHandler();
        JauntyConfig.RegisterTypeHandler<string>(handler);

        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entity = new TypeHandlerTestEntity { Name = "ClassHandlerTest" };

        ctx.Connection.Insert(entity);

        // Only comes back "CLASSHANDLED:"-prefixed if PrefixTypeHandler.Parse actually ran
        // during the read - the prior version of this test never asserted this, only that
        // the raw string it wrote via ADO.NET was unchanged by ADO.NET reading it back.
        var roundTripped = ctx.Connection.QueryFirst<TypeHandlerTestEntity>(
            $"SELECT * FROM {TableName} WHERE name = @Name", new { entity.Name });
        Assert.Equal("CLASSHANDLED:ClassHandlerTest", roundTripped.Name);
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

    // The following tests target write paths that build parameters from cached, compiled
    // property getters (WriteParameterCache, MultiRowInsertCache) or a raw explicit value
    // (SpParameters, entity-ID lookups) instead of going through Insert<T>'s ParameterBinder
    // call. Those cached paths used to assign getter output straight to IDbDataParameter.Value,
    // bypassing ApplyTypeHandlerIfNeeded entirely - a registered ITypeHandler or [EnumStorage]
    // attribute silently had no effect. Each test below exercises one specific bypassed call site.

    [Theory]
    [SystemSqlite]
    public void BulkInsert_SqliteLoopPath_EnumAttributeOverride_RoundTrips(DialectInfo dialect)
    {
        // SQLite always takes BulkInsertLoop (see BulkInsert.IsSqliteDialect), which binds
        // parameters via WriteParameterCache<T>.InsertValueSetter.
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entities = new[]
        {
            new TypeHandlerTestEntity { Name = "BulkInsertLoop1", EnumStringOverride = TestEnumForHandlers.Completed },
            new TypeHandlerTestEntity { Name = "BulkInsertLoop2", EnumStringOverride = TestEnumForHandlers.Cancelled }
        };

        ctx.Connection.BulkInsert(entities);

        var raw = ctx.Connection.QueryScalar<string>(
            $"SELECT enum_string_override FROM {TableName} WHERE name = @Name", new { Name = "BulkInsertLoop1" });
        Assert.Equal("Completed", raw);
    }

    [Theory]
    [SystemSqlite]
    public void BulkUpdate_WriteParameterCachePath_EnumAttributeOverride_RoundTrips(DialectInfo dialect)
    {
        // BulkUpdate always binds via WriteParameterCache<T>.UpdateValueSetter, regardless of dialect.
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entity = new TypeHandlerTestEntity { Name = "BulkUpdateTarget", EnumStringOverride = TestEnumForHandlers.Pending };
        ctx.Connection.Insert(entity);
        var inserted = ctx.Connection.QueryFirst<TypeHandlerTestEntity>(
            $"SELECT * FROM {TableName} WHERE name = @Name", new { entity.Name });

        inserted.EnumStringOverride = TestEnumForHandlers.Completed;
        ctx.Connection.BulkUpdate([inserted]);

        var raw = ctx.Connection.QueryScalar<string>(
            $"SELECT enum_string_override FROM {TableName} WHERE id = @Id", new { inserted.Id });
        Assert.Equal("Completed", raw);
    }

    [Theory]
    [MariaDB]
    public void BulkInsert_MultiRowPath_EnumAttributeOverride_RoundTrips(DialectInfo dialect)
    {
        // Non-SQLite dialects with SupportsMultiRowInsert and >1 entities take BulkInsertMultiRow,
        // which binds via MultiRowInsertCache.GetOrBuildGetters rather than WriteParameterCache.
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entities = new[]
        {
            new TypeHandlerTestEntity { Name = "BulkInsertMultiRow1", EnumStringOverride = TestEnumForHandlers.Completed },
            new TypeHandlerTestEntity { Name = "BulkInsertMultiRow2", EnumStringOverride = TestEnumForHandlers.Cancelled }
        };

        ctx.Connection.BulkInsert(entities);

        var raw = ctx.Connection.QueryScalar<string>(
            $"SELECT enum_string_override FROM {TableName} WHERE name = @Name", new { Name = "BulkInsertMultiRow1" });
        Assert.Equal("Completed", raw);
    }

    [Theory]
    [MariaDB]
    public async Task BulkInsertAsync_MultiRowPath_EnumAttributeOverride_RoundTrips(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entities = new[]
        {
            new TypeHandlerTestEntity { Name = "BulkInsertAsyncMultiRow1", EnumStringOverride = TestEnumForHandlers.Completed },
            new TypeHandlerTestEntity { Name = "BulkInsertAsyncMultiRow2", EnumStringOverride = TestEnumForHandlers.Cancelled }
        };

        await ctx.Connection.BulkInsertAsync(entities);

        var raw = ctx.Connection.QueryScalar<string>(
            $"SELECT enum_string_override FROM {TableName} WHERE name = @Name", new { Name = "BulkInsertAsyncMultiRow1" });
        Assert.Equal("Completed", raw);
    }

    [Theory]
    [SystemSqlite]
    public void Upsert_InsertBranch_EnumAttributeOverride_RoundTrips(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entity = new TypeHandlerTestEntity { Name = "UpsertInsert", EnumStringOverride = TestEnumForHandlers.Completed };
        ctx.Connection.Upsert(entity);

        var raw = ctx.Connection.QueryScalar<string>(
            $"SELECT enum_string_override FROM {TableName} WHERE name = @Name", new { entity.Name });
        Assert.Equal("Completed", raw);
    }

    [Theory]
    [SystemSqlite]
    public void GetById_RegisteredTypeHandlerForIdType_HandlerAppliedToIdParameter(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entity = new TypeHandlerTestEntity { Name = "GetByIdHandlerTest" };
        ctx.Connection.Insert(entity);
        var inserted = ctx.Connection.QueryFirst<TypeHandlerTestEntity>(
            $"SELECT * FROM {TableName} WHERE name = @Name", new { entity.Name });

        int toDbInvocations = 0;
        JauntyConfig.RegisterTypeHandler<long>(
            fromDb: dbValue => Convert.ToInt64(dbValue),
            toDb: value => { toDbInvocations++; return value; });
        try
        {
            var fetched = ctx.Connection.Get<TypeHandlerTestEntity>(inserted.Id);

            Assert.NotNull(fetched);
            Assert.True(toDbInvocations > 0);
        }
        finally
        {
            JauntyConfig.RemoveTypeHandler<long>();
        }
    }

    [Theory]
    [SystemSqlite]
    public void DeleteById_RegisteredTypeHandlerForIdType_HandlerAppliedToIdParameter(DialectInfo dialect)
    {
        using var ctx = _fixture.GetWriteContext(dialect);
        CreateTableForTest(ctx.Connection, dialect.Provider, TableName);

        var entity = new TypeHandlerTestEntity { Name = "DeleteByIdHandlerTest" };
        ctx.Connection.Insert(entity);
        var inserted = ctx.Connection.QueryFirst<TypeHandlerTestEntity>(
            $"SELECT * FROM {TableName} WHERE name = @Name", new { entity.Name });

        int toDbInvocations = 0;
        JauntyConfig.RegisterTypeHandler<long>(
            fromDb: dbValue => Convert.ToInt64(dbValue),
            toDb: value => { toDbInvocations++; return value; });
        try
        {
            int rowsDeleted = ctx.Connection.Delete<TypeHandlerTestEntity>(inserted.Id);

            Assert.Equal(1, rowsDeleted);
            Assert.True(toDbInvocations > 0);
        }
        finally
        {
            JauntyConfig.RemoveTypeHandler<long>();
        }
    }

    /// <summary>
    /// UpdateProductPrice's NewPrice parameter name. PostgreSQL/MariaDB use p_ prefix with different casing.
    /// </summary>
    private static string NewPriceParamName(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? "p_new_price" :
        dialect.Provider == DialectProvider.MariaDb ? "p_NewPrice" : "NewPrice";

    private static string ProductIdParamName(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? "p_product_id" :
        dialect.Provider == DialectProvider.MariaDb ? "p_ProductId" : "ProductId";

    private static string SpName(string name, DialectInfo dialect) =>
        dialect.Provider == DialectProvider.Postgres ? name.ToLower() : name;

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void ExecuteStoredProcedureNonQuery_SpParametersExplicitValue_TypeHandlerApplied(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var transaction = connection.BeginTransaction();

        int toDbInvocations = 0;
        JauntyConfig.RegisterTypeHandler<decimal>(
            fromDb: dbValue => Convert.ToDecimal(dbValue),
            toDb: value => { toDbInvocations++; return value; });
        try
        {
            var parameters = new SpParameters()
                .AddInput(ProductIdParamName(dialect), 1)
                .AddInput(NewPriceParamName(dialect), 55.55m);

            connection.ExecuteStoredProcedureNonQuery(
                SpName("UpdateProductPrice", dialect),
                parameters,
                CommandOptions.WithTransaction(transaction));

            // BindSpParameters used to assign sp.Value straight to the ADO.NET parameter,
            // bypassing any registered ITypeHandler entirely - toDb would never run.
            Assert.True(toDbInvocations > 0);
        }
        finally
        {
            transaction.Rollback();
            JauntyConfig.RemoveTypeHandler<decimal>();
        }
    }

}