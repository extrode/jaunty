using System.Data;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Fluent.Tests.Entities;

namespace Jaunty.Fluent.Tests.Unit.Builders.Join;

/// <summary>
/// Regression test (round 10): JoinClauseBuilder.On&lt;TValue&gt;(condition, value) used to
/// hardcode the bound parameter name as "@value" regardless of dialect, unlike every other
/// parameter-name construction in the project. A dialect with a non-"@" parameter prefix (e.g.
/// DuckDB's "$") would end up with a bound parameter name that didn't match the "$value"
/// placeholder a caller wrote in their raw condition string for that dialect.
/// </summary>
public class JoinClauseBuilderParameterPrefixTests
{
    [Fact]
    public void On_TypedValue_UsesDialectParameterPrefix()
    {
        SqlDialectFactory.RegisterDialect(nameof(DollarPrefixConnection), new DollarPrefixDialect());
        var connection = new DollarPrefixConnection();

        var joinedQuery = connection.From<Product>()
            .InnerJoin<Category>()
            .On<int>("p.category_id = c.category_id AND p.active = $value", 1);

        var builder = Assert.IsType<JoinedQueryBuilder<Product, Category>>(joinedQuery);
        var parameters = builder.GetParameters().GetAll();

        Assert.Contains(parameters, p => p.Name == "$value" && Equals(p.Value, 1));
        Assert.DoesNotContain(parameters, p => p.Name == "@value");
    }

    private sealed class DollarPrefixConnection : IDbConnection
    {
        public string ConnectionString { get => ""; set { } }
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

    private sealed class DollarPrefixDialect : ISqlDialect
    {
        private readonly SQLiteDialect _inner = new();

        public string ParameterPrefix => "$";

        public bool SupportsForeignKeyToggle => _inner.SupportsForeignKeyToggle;
        public bool SupportsUpsert => _inner.SupportsUpsert;
        public bool SupportsMultiRowInsert => _inner.SupportsMultiRowInsert;
        public bool SupportsNativeBulkCopy => _inner.SupportsNativeBulkCopy;
        public int MaxParametersPerStatement => _inner.MaxParametersPerStatement;
        public IBulkCopyProvider? CreateBulkCopyProvider() => _inner.CreateBulkCopyProvider();
        public string? GetDisableForeignKeyChecksSql() => _inner.GetDisableForeignKeyChecksSql();
        public string? GetEnableForeignKeyChecksSql() => _inner.GetEnableForeignKeyChecksSql();

        public string GetDefaultSchema() => _inner.GetDefaultSchema();
        public string EscapeTableName(string? schemaName, string tableName) => _inner.EscapeTableName(schemaName, tableName);
        public string EscapeColumnName(string columnName) => _inner.EscapeColumnName(columnName);
        public string GetLastInsertIdSql(params string[] columnNames) => _inner.GetLastInsertIdSql(columnNames);
        public string GetPagingSql(string baseSql, int offset, int fetchNext) => _inner.GetPagingSql(baseSql, offset, fetchNext);
        public bool IsKeyword(string identifier) => _inner.IsKeyword(identifier);
        public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseSensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar) => _inner.GenerateCaseInsensitiveLike(columnName, parameterName, escapeChar);
        public string GenerateCaseInsensitiveEquals(string columnName, string parameterName) => _inner.GenerateCaseInsensitiveEquals(columnName, parameterName);
        public string FormatContainsPattern(string value) => _inner.FormatContainsPattern(value);
        public string FormatStartsWithPattern(string value) => _inner.FormatStartsWithPattern(value);
        public string FormatEndsWithPattern(string value) => _inner.FormatEndsWithPattern(value);
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
