using System.Data;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Interfaces;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Write;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R35-013. <c>RegisterDialect</c> cleared the resolved-dialect cache but not the caches built
/// from what that dialect produced, so one prior CRUD operation for an (entity, connection) pair
/// pinned its SQL - quoting, upsert form, identity SQL - to the dialect in force at the time, for
/// the life of the process.
/// </summary>
[Collection("Dialect Factory State")]
public class DialectRegistrationInvalidatesDerivedCachesTests : IDisposable
{
    private readonly FakeConnection _connection = new();

    public void Dispose()
    {
        SqlDialectFactory.ResetRegistrations();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Table("widgets")]
    public class Widget
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void RegisteringADialectAfterTheSqlWasCached_RebuildsWithTheNewDialect()
    {
        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('<', '>'));
        CachedCrudSql first = CrudSqlCache.GetSql<Widget>(_connection);
        Assert.Contains("<widgets>", first.InsertSql);

        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('{', '}'));
        CachedCrudSql second = CrudSqlCache.GetSql<Widget>(_connection);

        Assert.Contains("{widgets}", second.InsertSql);
        Assert.DoesNotContain("<widgets>", second.InsertSql);
    }

    [Fact]
    public void TheNameKeyedOverloadInvalidatesTheSameWay()
    {
        SqlDialectFactory.RegisterDialect(nameof(FakeConnection), new QuotingDialect('<', '>'));
        Assert.Contains("<id>", CrudSqlCache.GetSql<Widget>(_connection).SelectByIdSql);

        SqlDialectFactory.RegisterDialect(nameof(FakeConnection), new QuotingDialect('{', '}'));

        Assert.Contains("{id}", CrudSqlCache.GetSql<Widget>(_connection).SelectByIdSql);
    }

    [Fact]
    public void EveryCachedStatementIsRebuilt_NotJustTheOneReadBack()
    {
        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('<', '>'));
        CrudSqlCache.GetSql<Widget>(_connection);

        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('{', '}'));
        CachedCrudSql sql = CrudSqlCache.GetSql<Widget>(_connection);

        foreach (string statement in new[]
        {
            sql.InsertSql, sql.UpdateSql, sql.DeleteSql, sql.DeleteByIdSql,
            sql.SelectByIdSql, sql.SelectAllSql,
        })
        {
            Assert.DoesNotContain("<", statement);
            Assert.Contains("{", statement);
        }
    }

    [Fact]
    public void WithNoRegistrationInBetween_TheCacheStillHits()
    {
        // The other half: invalidating on registration must not turn every lookup into a rebuild.
        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('<', '>'));

        CachedCrudSql first = CrudSqlCache.GetSql<Widget>(_connection);
        CachedCrudSql second = CrudSqlCache.GetSql<Widget>(_connection);

        Assert.Same(first, second);
    }

    [Fact]
    public void ResolutionItselfAlsoFollowsTheNewRegistration()
    {
        // Pins the pre-existing half of the behaviour, so a change to the invalidation cannot
        // regress the _dialectCache clearing that AUD-R26 added.
        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('<', '>'));
        Assert.Equal("<x>", SqlDialectFactory.GetDialect(_connection).EscapeColumnName("x"));

        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('{', '}'));

        Assert.Equal("{x}", SqlDialectFactory.GetDialect(_connection).EscapeColumnName("x"));
    }

    // -----------------------------------------------------------------------------
    // AUD-R35-128: how many times the identity SQL is built per cache miss
    // -----------------------------------------------------------------------------

    [Table("identity_widgets")]
    public class IdentityWidget
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// AUD-R35-128. <c>BuildCachedSql</c> built the no-identity form unconditionally and then threw
    /// it away and rebuilt from the trimmed column array whenever the entity had an identity key -
    /// two string builds per cache miss for the common case, and a call that read as load-bearing
    /// when it was only the no-identity placeholder.
    /// </summary>
    [Fact]
    public void AnEntityWithAnIdentityKey_BuildsTheIdentitySqlOnce_FromItsOwnColumns()
    {
        var dialect = new QuotingDialect('<', '>');
        SqlDialectFactory.RegisterDialect<FakeConnection>(dialect);

        CrudSqlCache.GetSql<IdentityWidget>(_connection);

        string[] columns = Assert.Single(dialect.LastInsertIdCalls);
        Assert.Equal(["<id>"], columns);
    }

    [Fact]
    public void AnEntityWithNoIdentityKey_StillGetsThePlaceholderForm()
    {
        var dialect = new QuotingDialect('<', '>');
        SqlDialectFactory.RegisterDialect<FakeConnection>(dialect);

        CrudSqlCache.GetSql<Widget>(_connection);

        Assert.Empty(Assert.Single(dialect.LastInsertIdCalls));
    }

    /// <summary>
    /// AUD-R35-135. <c>MultiRowInsertCache</c> is the same shape as <c>CrudSqlCache</c> - keyed on
    /// (entity type, connection type, batch size), holding SQL built entirely from the dialect, and
    /// invalidated only by the configuration generation. It had no test that a dialect registration
    /// retires it, so the mechanism AUD-R35-013 installed was covered for one cache and assumed for
    /// the other.
    /// </summary>
    [Fact]
    public void MultiRowInsertSql_IsRebuiltAfterADialectRegistration()
    {
        var metadata = new EntityMetadata("widgets", null,
        [
            new ColumnMetadata("Name", typeof(string), "name", isPrimaryKey: false, isIdentity: false,
                isComputed: false, getter: _ => "n", setter: (_, _) => { })
        ]);

        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('<', '>'));
        string first = MultiRowInsertCache.GetOrBuild(
            typeof(Widget), typeof(FakeConnection), 2, metadata, SqlDialectFactory.GetDialect(_connection));

        SqlDialectFactory.RegisterDialect<FakeConnection>(new QuotingDialect('{', '}'));
        string second = MultiRowInsertCache.GetOrBuild(
            typeof(Widget), typeof(FakeConnection), 2, metadata, SqlDialectFactory.GetDialect(_connection));

        Assert.Contains("<widgets>", first, StringComparison.Ordinal);
        Assert.Contains("{widgets}", second, StringComparison.Ordinal);
    }

    private sealed class QuotingDialect(char open, char close) : ISqlDialect
    {
        private readonly SQLiteDialect _inner = new();

        internal List<string[]> LastInsertIdCalls { get; } = [];

        public bool SupportsNativeBulkCopy => false;
        public IBulkCopyProvider? CreateBulkCopyProvider() => null;
        public bool SupportsForeignKeyToggle => _inner.SupportsForeignKeyToggle;
        public bool RequiresAutocommitForForeignKeyToggle => _inner.RequiresAutocommitForForeignKeyToggle;
        public bool SupportsUpsert => _inner.SupportsUpsert;
        public bool SupportsMultiRowInsert => _inner.SupportsMultiRowInsert;
        public int MaxParametersPerStatement => _inner.MaxParametersPerStatement;
        public string ParameterPrefix => _inner.ParameterPrefix;
        public string GetDefaultSchema() => _inner.GetDefaultSchema();
        public string EscapeTableName(string? schemaName, string tableName) =>
            schemaName is null ? Quote(tableName) : Quote(schemaName) + "." + Quote(tableName);
        public string EscapeColumnName(string columnName) => Quote(columnName);
        public string EscapeStringLiteral(string value) => _inner.EscapeStringLiteral(value);
        public string GetLastInsertIdSql(params string[] columnNames)
        {
            LastInsertIdCalls.Add(columnNames);
            return _inner.GetLastInsertIdSql(columnNames);
        }
        public string GetPagingSql(string baseSql, int offset, int fetchNext) => _inner.GetPagingSql(baseSql, offset, fetchNext);
        public bool IsKeyword(string identifier) => _inner.IsKeyword(identifier);
        public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseSensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseInsensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveEquals(string columnName, string parameterName) => _inner.GenerateCaseInsensitiveEquals(columnName, parameterName);
        public string FormatContainsPattern(string value) => _inner.FormatContainsPattern(value);
        public string FormatStartsWithPattern(string value) => _inner.FormatStartsWithPattern(value);
        public string FormatEndsWithPattern(string value) => _inner.FormatEndsWithPattern(value);
        public string FormatBooleanLiteral(bool value) => _inner.FormatBooleanLiteral(value);
        public string? GetDisableForeignKeyChecksSql() => _inner.GetDisableForeignKeyChecksSql();
        public string? GetEnableForeignKeyChecksSql() => _inner.GetEnableForeignKeyChecksSql();
        public string GenerateCoalesce(params string[] expressions) => _inner.GenerateCoalesce(expressions);
        public string GenerateIsNull(string expression, string defaultExpression) => _inner.GenerateIsNull(expression, defaultExpression);
        public string GenerateNullIf(string expression, string compareExpression) => _inner.GenerateNullIf(expression, compareExpression);
        public string GenerateLength(string expression) => _inner.GenerateLength(expression);
        public string GenerateUpper(string expression) => _inner.GenerateUpper(expression);
        public string GenerateLower(string expression) => _inner.GenerateLower(expression);
        public string GenerateTrim(string expression) => _inner.GenerateTrim(expression);
        public string GenerateSubstring(string expression, string start, string length) => _inner.GenerateSubstring(expression, start, length);
        public string GenerateYear(string expression) => _inner.GenerateYear(expression);
        public string GenerateMonth(string expression) => _inner.GenerateMonth(expression);
        public string GenerateDay(string expression) => _inner.GenerateDay(expression);
        public string GenerateUpsertSql(string tableName, string[] insertColumns, string[] insertParams, string[] updateColumns, string[] updateParams, string[] keyColumns, string[] keyParams) => _inner.GenerateUpsertSql(tableName, insertColumns, insertParams, updateColumns, updateParams, keyColumns, keyParams);
        public string GenerateRowNumber() => _inner.GenerateRowNumber();
        public string GenerateRank() => _inner.GenerateRank();
        public string GenerateDenseRank() => _inner.GenerateDenseRank();
        public string GenerateNTile(int buckets) => _inner.GenerateNTile(buckets);
        public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy) => _inner.GenerateOverClause(partitionBy, orderBy);
        public string GenerateWindowAggregate(string function, string? expression) => _inner.GenerateWindowAggregate(function, expression);

        private string Quote(string name) => string.Concat(open.ToString(), name, close.ToString());
    }

    private sealed class FakeConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public ConnectionState State => ConnectionState.Open;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Dispose() { }
        public void Open() { }
    }
}
