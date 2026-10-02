using System.Data;

using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Dialects;
using Extrode.Jaunty.Fluent.Internals;
using Extrode.Jaunty.Fluent.Tests.Entities;

namespace Extrode.Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// AUD-R38-088. The joined select lists escaped every column of every entity on each terminal,
/// bypassing the per-dialect cache the other column-reference sites read from.
/// </summary>
public class JoinedSelectColumnEscapeCacheTests
{
    private readonly EscapeCountingDialect _dialect = new();
    private readonly EscapeCountingConnection _connection = new();

    public JoinedSelectColumnEscapeCacheTests()
    {
        SqlDialectFactory.RegisterDialect(nameof(EscapeCountingConnection), _dialect);
    }

    private int CallsFor(Func<string> build)
    {
        build();
        int before = _dialect.EscapeColumnNameCalls;
        string sql = build();
        Assert.Contains("SELECT", sql, StringComparison.Ordinal);
        return _dialect.EscapeColumnNameCalls - before;
    }

    [Fact]
    public void TwoTableSelectList_ReusesTheCachedEscapes()
    {
        Assert.Equal(0, CallsFor(() => _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql()));
    }

    [Fact]
    public void ThreeTableSelectList_ReusesTheCachedEscapes()
    {
        Assert.Equal(0, CallsFor(() => _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .InnerJoin<Supplier>()
            .On(p => p.SupplierId, s => s.SupplierId)
            .ToSql()));
    }

    [Fact]
    public void TheCachedSelectList_KeepsTheEntityColumnOrder()
    {
        string sql = _connection.From<Product>()
            .InnerJoin<Category>()
            .On(p => p.CategoryId, c => c.CategoryId)
            .ToSql();

        string expected = string.Join(", ", FluentMetadataCache.GetMetadata<Product>().Columns
            .Select(c => "p." + _dialect.EscapeColumnName(c.ColumnName)));
        Assert.Contains(expected, sql, StringComparison.Ordinal);
    }

    private sealed class EscapeCountingConnection : IDbConnection
    {
        #pragma warning disable CS8767 // IDbConnection.ConnectionString is [AllowNull]; the attribute is not public on net472.
        public string ConnectionString { get => ""; set { } }
        #pragma warning restore CS8767
        public int ConnectionTimeout => 0;
        public string Database => "";
        public ConnectionState State => ConnectionState.Closed;
        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }

    private sealed class EscapeCountingDialect : ISqlDialect
    {
        private readonly SQLiteDialect _inner = new();

        public int EscapeColumnNameCalls;

        public string ParameterPrefix => _inner.ParameterPrefix;

        public bool SupportsForeignKeyToggle => _inner.SupportsForeignKeyToggle;
        public bool RequiresAutocommitForForeignKeyToggle => _inner.RequiresAutocommitForForeignKeyToggle;
        public bool UpsertBatchIsAtomic => _inner.UpsertBatchIsAtomic;
        public bool SupportsUpsert => _inner.SupportsUpsert;
        public bool SupportsMultiRowInsert => _inner.SupportsMultiRowInsert;
        public bool SupportsNativeBulkCopy => _inner.SupportsNativeBulkCopy;
        public int MaxParametersPerStatement => _inner.MaxParametersPerStatement;
        public IBulkCopyProvider? CreateBulkCopyProvider() => _inner.CreateBulkCopyProvider();
        public string? GetDisableForeignKeyChecksSql() => _inner.GetDisableForeignKeyChecksSql();
        public string? GetEnableForeignKeyChecksSql() => _inner.GetEnableForeignKeyChecksSql();

        public string GetDefaultSchema() => _inner.GetDefaultSchema();
        public string EscapeTableName(string? schemaName, string tableName) => _inner.EscapeTableName(schemaName, tableName);
        public string EscapeColumnName(string columnName)
        {
            EscapeColumnNameCalls++;
            return _inner.EscapeColumnName(columnName);
        }
        public string EscapeStringLiteral(string value) => _inner.EscapeStringLiteral(value);
        public string GetLastInsertIdSql(params string[] columnNames) => _inner.GetLastInsertIdSql(columnNames);
        public string GetPagingSql(string baseSql, int offset, int fetchNext) => _inner.GetPagingSql(baseSql, offset, fetchNext);
        public bool IsKeyword(string identifier) => _inner.IsKeyword(identifier);
        public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseSensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseInsensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveEquals(string columnName, string parameterName) => _inner.GenerateCaseInsensitiveEquals(columnName, parameterName);
        public string FormatContainsPattern(string value) => _inner.FormatContainsPattern(value);
        public string FormatStartsWithPattern(string value) => _inner.FormatStartsWithPattern(value);
        public string FormatEndsWithPattern(string value) => _inner.FormatEndsWithPattern(value);
        public string FormatBooleanLiteral(bool value) => _inner.FormatBooleanLiteral(value);
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
    }
}
