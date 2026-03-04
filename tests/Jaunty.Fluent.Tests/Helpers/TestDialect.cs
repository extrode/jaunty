using Jaunty.Internals.BulkCopy;
using Jaunty.Dialects;

namespace Jaunty.Fluent.Tests.Helpers;

/// <summary>
/// A test SQL dialect that generates generic ANSI SQL.
/// Used for unit testing expression visitors without database dependencies.
/// </summary>
internal class TestDialect : ISqlDialect
{
    public bool SupportsForeignKeyToggle => false;
    public bool SupportsUpsert => true;
    public bool SupportsMultiRowInsert => true;
    public int MaxParametersPerStatement => 2100;
    public bool SupportsNativeBulkCopy => false;

    public IBulkCopyProvider? CreateBulkCopyProvider() => null;

    public string GetDefaultSchema() => "dbo";

    public string EscapeTableName(string? schemaName, string tableName)
    {
        return schemaName != null ? $"[{schemaName}].[{tableName}]" : $"[{tableName}]";
    }

    public string EscapeColumnName(string columnName) => $"[{columnName}]";

    public string GetLastInsertIdSql(params string[] columnNames) => "SELECT SCOPE_IDENTITY()";

    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        return $"{baseSql} ORDER BY (SELECT NULL) OFFSET {offset} ROWS FETCH NEXT {fetchNext} ROWS ONLY";
    }

    public bool IsKeyword(string identifier) => false;

    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        return $"{columnName} LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        return $"LOWER({columnName}) LIKE LOWER({parameterName}) ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        return $"LOWER({columnName}) = LOWER({parameterName})";
    }

    public string FormatContainsPattern(string value) => $"%{value}%";
    public string FormatStartsWithPattern(string value) => $"{value}%";
    public string FormatEndsWithPattern(string value) => $"%{value}";

    public string? GetDisableForeignKeyChecksSql() => null;
    public string? GetEnableForeignKeyChecksSql() => null;

    public string GenerateCoalesce(params string[] expressions)
    {
        return $"COALESCE({string.Join(", ", expressions)})";
    }

    public string GenerateIsNull(string expression, string defaultExpression)
    {
        return $"CASE WHEN {expression} IS NULL THEN {defaultExpression} ELSE {expression} END";
    }

    public string GenerateNullIf(string expression, string compareExpression)
    {
        return $"NULLIF({expression}, {compareExpression})";
    }

    public string GenerateLength(string expression) => $"LEN({expression})";
    public string GenerateUpper(string expression) => $"UPPER({expression})";
    public string GenerateLower(string expression) => $"LOWER({expression})";
    public string GenerateTrim(string expression) => $"TRIM({expression})";
    public string GenerateSubstring(string expression, string start, string length) => $"SUBSTRING({expression}, {start}, {length})";

    public string GenerateYear(string expression) => $"YEAR({expression})";
    public string GenerateMonth(string expression) => $"MONTH({expression})";
    public string GenerateDay(string expression) => $"DAY({expression})";

    public string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns)
    {
        var columnsList = string.Join(", ", insertColumns);
        var valuesList = string.Join(", ", insertParams);
        var updateSet = string.Join(", ", updateColumns.Select((col, i) => $"{col} = {updateParams[i]}"));
        var keys = string.Join(" AND ", keyColumns.Select(k => $"target.{k} = source.{k}"));

        return $"""
            MERGE {tableName} AS target
            USING (SELECT {valuesList}) AS source
            ON {keys}
            WHEN MATCHED THEN UPDATE SET {updateSet}
            WHEN NOT MATCHED THEN INSERT ({columnsList}) VALUES ({valuesList});
            """;
    }

    public string GenerateRowNumber() => "ROW_NUMBER() OVER ()";
    public string GenerateRank() => "RANK() OVER ()";
    public string GenerateDenseRank() => "DENSE_RANK() OVER ()";
    public string GenerateNTile(int buckets) => $"NTILE({buckets}) OVER ()";

    public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy)
    {
        var sql = "OVER (";
        if (partitionBy != null && partitionBy.Length > 0)
            sql += $"PARTITION BY {string.Join(", ", partitionBy)} ";
        if (orderBy != null && orderBy.Length > 0)
            sql += $"ORDER BY {string.Join(", ", orderBy.Select(o => o.column + (o.descending ? " DESC" : " ASC")))} ";
        sql += ")";
        return sql;
    }

    public string GenerateWindowAggregate(string function, string? expression)
    {
        var arg = expression ?? "*";
        return $"{function}({arg})";
    }
}
