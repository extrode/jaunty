using Jaunty.Dialects;
using Jaunty.Extensions.Reflection.BulkCopy;
using Jaunty.Configuration;

namespace Jaunty.Extensions.Reflection.Dialects;

/// <summary>
/// PostgreSQL dialect with bulk copy support.
/// Extends the base dialect to provide NpgsqlBinaryImporter provider.
/// </summary>
internal sealed class PostgreSqlDialectWithBulkCopy : ISqlDialect, ISubstringToEndDialect, IDialectWrapper
{
    private readonly PostgreSqlDialect _inner;

    public PostgreSqlDialectWithBulkCopy(PostgreSqlDialect? inner = null)
    {
        _inner = inner ?? new PostgreSqlDialect();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Exposes the decorated dialect so engine-identity tests (CSV import dispatch,
    /// BulkInsert's SQLite routing) still see PostgreSqlDialect once UseNativeBulkCopy()
    /// has swapped this wrapper in - see SqlDialectFactory.Unwrap.
    /// </remarks>
    public ISqlDialect InnerDialect => _inner;

    public bool SupportsNativeBulkCopy => true;

    public IBulkCopyProvider? CreateBulkCopyProvider()
    {
        return new PostgreSqlBulkCopyProvider();
    }

    // Delegate all other calls to the base dialect
    public bool SupportsForeignKeyToggle => _inner.SupportsForeignKeyToggle;
    public bool RequiresAutocommitForForeignKeyToggle => _inner.RequiresAutocommitForForeignKeyToggle;
    public bool SupportsUpsert => _inner.SupportsUpsert;
    public bool SupportsMultiRowInsert => _inner.SupportsMultiRowInsert;
    public int MaxParametersPerStatement => _inner.MaxParametersPerStatement;

    public string ParameterPrefix => _inner.ParameterPrefix;
    public string GetDefaultSchema() => _inner.GetDefaultSchema();
    public string EscapeTableName(string? schemaName, string tableName) => _inner.EscapeTableName(schemaName, tableName);
    public string EscapeColumnName(string columnName) => _inner.EscapeColumnName(columnName);
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

    /// <summary>
    /// Forwards to the wrapped dialect. A wrapper that silently dropped this would make the wrapped
    /// dialect look like one that never had it, and the caller would fall back to a sentinel length
    /// - exactly the defect this interface exists to remove. Because the compiler cannot enforce an
    /// optional interface, <c>SubstringToEndDialectTests</c> pins it instead.
    /// </summary>
    public string GenerateSubstringToEnd(string expression, string start)
        => SubstringToEnd.Generate(_inner, expression, start);
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