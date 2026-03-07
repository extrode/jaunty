using System.Text;

using Jaunty.Dialects;
using Jaunty.Internals.BulkCopy;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// SQL dialect for DuckDB, implementing both <see cref="ISqlDialect"/> for standard SQL generation
/// and <see cref="IFlatFileDialect"/> for flat file-specific operations.
/// </summary>
public sealed class DuckDbDialect : IFlatFileDialect
{
    /// <summary>
    /// Singleton instance of the DuckDB dialect.
    /// </summary>
    public static readonly DuckDbDialect Instance = new();

    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL", "ANALYSE", "ANALYZE", "AND", "ANY", "ARRAY", "AS", "ASC", "ASYMMETRIC",
        "AUTHORIZATION", "BETWEEN", "BIGINT", "BINARY", "BIT", "BOOLEAN", "BOTH", "CASE",
        "CAST", "CHAR", "CHARACTER", "CHECK", "COALESCE", "COLLATE", "COLLATION", "COLUMN",
        "CONSTRAINT", "CREATE", "CROSS", "CURRENT_CATALOG", "CURRENT_DATE",
        "CURRENT_ROLE", "CURRENT_SCHEMA", "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_USER",
        "DEC", "DECIMAL", "DEFAULT", "DEFERRABLE", "DELETE", "DESC", "DESCRIBE", "DISTINCT",
        "DO", "ELSE", "END", "EXCEPT", "EXISTS", "EXPLAIN", "EXPORT", "EXTRACT",
        "FALSE", "FETCH", "FLOAT", "FOR", "FOREIGN", "FROM", "FULL", "GRANT", "GROUP",
        "HAVING", "ILIKE", "IMPORT", "IN", "INITIALLY", "INNER", "INOUT", "INSERT", "INT",
        "INTEGER", "INTERSECT", "INTERVAL", "INTO", "IS", "ISNULL", "JOIN",
        "LATERAL", "LEADING", "LEFT", "LIKE", "LIMIT", "LOCALTIME", "LOCALTIMESTAMP",
        "NATURAL", "NOT", "NOTNULL", "NULL", "NULLIF", "NUMERIC", "OFFSET",
        "ON", "ONLY", "OR", "ORDER", "OUT", "OUTER", "OVERLAPS", "PIVOT",
        "POSITION", "PRECISION", "PRIMARY", "REAL", "REFERENCES", "RETURNING", "RIGHT",
        "ROW", "SELECT", "SESSION_USER", "SIMILAR", "SMALLINT", "SOME", "STRUCT",
        "SUBSTRING", "SYMMETRIC", "TABLE", "THEN", "TIME", "TIMESTAMP", "TO",
        "TRAILING", "TRIM", "TRUE", "TRY_CAST", "UNION", "UNIQUE", "UNPIVOT",
        "UPDATE", "USER", "USING", "VALUES", "VARCHAR", "WHEN", "WHERE", "WINDOW", "WITH"
    };

    public string ParameterPrefix => "$";

    public string GetDefaultSchema() => "main";

    public bool IsKeyword(string identifier) => identifier is not null && Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        // DuckDB uses double-quote escaping like PostgreSQL
        // Always quote identifiers for safety with flat file column names
        var escapedTable = $"\"{tableName}\"";

        if (string.IsNullOrWhiteSpace(schemaName))
            return escapedTable;

        return $"\"{schemaName}\".{escapedTable}";
    }

    public string EscapeColumnName(string columnName)
    {
        // If already escaped (starts and ends with quotes), return as-is to prevent double-escaping.
        // This is necessary because CachedDialectMetadata pre-escapes column names,
        // and BuildSelectSql calls EscapeColumnName again on those cached values.
        if (columnName.Length >= 2 && columnName[0] == '"' && columnName[^1] == '"')
            return columnName;

        return $"\"{columnName}\"";
    }

    public string GetLastInsertIdSql(params string[] columnNames)
    {
        // DuckDB does not have a direct last_insert_id equivalent
        // Use RETURNING clause instead (handled by the caller)
        if (columnNames.Length == 0) return "RETURNING *;";
        return $"RETURNING {string.Join(", ", columnNames)};";
    }

    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        return $"{baseSql} LIMIT {fetchNext} OFFSET {offset}";
    }

    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // DuckDB: LIKE is case-sensitive by default (like PostgreSQL)
        return $"{columnName} LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // DuckDB supports ILIKE (like PostgreSQL)
        return $"{columnName} ILIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        return $"LOWER({columnName}) = LOWER({parameterName})";
    }

    public string FormatContainsPattern(string value) => $"%{value}%";
    public string FormatStartsWithPattern(string value) => $"{value}%";
    public string FormatEndsWithPattern(string value) => $"%{value}";

    // DuckDB does not support session-level FK toggling
    public string? GetDisableForeignKeyChecksSql() => null;
    public string? GetEnableForeignKeyChecksSql() => null;
    public bool SupportsForeignKeyToggle => false;

    public string GenerateCoalesce(params string[] expressions)
    {
        return $"COALESCE({string.Join(", ", expressions)})";
    }

    public string GenerateIsNull(string expression, string defaultExpression)
    {
        // DuckDB uses COALESCE (same as PostgreSQL)
        return $"COALESCE({expression}, {defaultExpression})";
    }

    public string GenerateNullIf(string expression, string compareExpression)
    {
        return $"NULLIF({expression}, {compareExpression})";
    }

    // String functions
    public string GenerateLength(string expression) => $"LENGTH({expression})";
    public string GenerateUpper(string expression) => $"UPPER({expression})";
    public string GenerateLower(string expression) => $"LOWER({expression})";
    public string GenerateTrim(string expression) => $"TRIM({expression})";
    public string GenerateSubstring(string expression, string start, string length) => $"SUBSTRING({expression}, {start}, {length})";

    // Date functions - DuckDB uses EXTRACT (like PostgreSQL)
    public string GenerateYear(string expression) => $"EXTRACT(YEAR FROM {expression})";
    public string GenerateMonth(string expression) => $"EXTRACT(MONTH FROM {expression})";
    public string GenerateDay(string expression) => $"EXTRACT(DAY FROM {expression})";

    // Multi-row insert support
    public bool SupportsMultiRowInsert => true;
    public int MaxParametersPerStatement => 32768;

    // Upsert support - DuckDB supports INSERT OR REPLACE and ON CONFLICT
    public bool SupportsUpsert => true;

    public string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns)
    {
        var sb = new StringBuilder(256);
        sb.Append("INSERT INTO ");
        sb.Append(tableName);
        sb.Append(" (");

        for (int i = 0; i < insertColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(insertColumns[i]);
        }

        sb.Append(") VALUES (");
        for (int i = 0; i < insertParams.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(insertParams[i]);
        }

        sb.Append(") ON CONFLICT (");
        for (int i = 0; i < keyColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(keyColumns[i]);
        }

        sb.Append(") DO UPDATE SET ");

        for (int i = 0; i < updateColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(updateColumns[i]);
            sb.Append(" = EXCLUDED.");
            sb.Append(updateColumns[i]);
        }

        return sb.ToString();
    }

    // Window functions - DuckDB supports standard SQL window functions
    public string GenerateRowNumber() => "ROW_NUMBER()";
    public string GenerateRank() => "RANK()";
    public string GenerateDenseRank() => "DENSE_RANK()";
    public string GenerateNTile(int buckets) => $"NTILE({buckets})";

    public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy)
    {
        var sb = new StringBuilder(64);
        sb.Append(" OVER (");

        if (partitionBy is { Length: > 0 })
        {
            sb.Append("PARTITION BY ");
            for (int i = 0; i < partitionBy.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(partitionBy[i]);
            }
        }

        if (orderBy is { Length: > 0 })
        {
            if (partitionBy is { Length: > 0 }) sb.Append(' ');
            sb.Append("ORDER BY ");
            for (int i = 0; i < orderBy.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(orderBy[i].column);
                if (orderBy[i].descending) sb.Append(" DESC");
            }
        }

        sb.Append(')');
        return sb.ToString();
    }

    public string GenerateWindowAggregate(string function, string? expression)
    {
        return expression is null ? $"{function}(*)" : $"{function}({expression})";
    }

    // DuckDB has no native bulk copy API accessible via ADO.NET
    public bool SupportsNativeBulkCopy => false;

    public IBulkCopyProvider? CreateBulkCopyProvider() => null;

    // ==========================================
    // IFlatFileDialect Implementation
    // ==========================================

    public string GenerateCreateViewSql(IFileSource source)
    {
        var readFunction = GenerateReadFunction(source);
        return $"CREATE OR REPLACE VIEW \"{source.TableName}\" AS SELECT * FROM {readFunction}";
    }

    public string GenerateCreateTableAsSql(IFileSource source)
    {
        var readFunction = GenerateReadFunction(source);
        return $"CREATE OR REPLACE TABLE \"{source.TableName}\" AS SELECT * FROM {readFunction}";
    }

    public string GeneratePromoteToTableSql(IFileSource source)
    {
        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE \"{source.TableName}_tmp\" AS SELECT * FROM \"{source.TableName}\"; ");
        sb.Append($"DROP VIEW \"{source.TableName}\"; ");
        sb.Append($"ALTER TABLE \"{source.TableName}_tmp\" RENAME TO \"{source.TableName}\";");
        return sb.ToString();
    }

    public string GenerateCopyToSql(string tableName, string outputPath, string format)
    {
        ArgumentNullException.ThrowIfNull(format);

        var escapedPath = outputPath.Replace("'", "''");

        // Map logical format to DuckDB format name
        var duckDbFormat = format.ToUpperInvariant() switch
        {
            "CSV" => "CSV",
            "TSV" => "CSV",
            "PARQUET" => "PARQUET",
            "JSON" => "JSON",
            "XLSX" => "XLSX",
            _ => format.ToUpperInvariant() // Pass through for custom formats
        };

        var sb = new StringBuilder();
        sb.Append($"COPY \"{tableName}\" TO '{escapedPath}' (FORMAT {duckDbFormat}");

        if (string.Equals(format, FileFormats.Tsv, StringComparison.OrdinalIgnoreCase))
            sb.Append(", DELIMITER '\t'");

        // HEADER is only valid for CSV/TSV
        if (string.Equals(format, FileFormats.Csv, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format, FileFormats.Tsv, StringComparison.OrdinalIgnoreCase))
            sb.Append(", HEADER true");

        sb.Append(')');
        return sb.ToString();
    }

    public string GenerateCopyToSql(string tableName, string outputPath, IFileSource source)
    {
        var escapedPath = outputPath.Replace("'", "''");

        var sb = new StringBuilder();
        sb.Append($"COPY \"{tableName}\" TO '{escapedPath}' (FORMAT {source.DuckDbFormatName}");

        var extraOptions = source.GenerateCopyToOptions();
        if (!string.IsNullOrEmpty(extraOptions))
        {
            sb.Append(", ");
            sb.Append(extraOptions);
        }

        sb.Append(')');
        return sb.ToString();
    }

    internal static string GenerateReadFunction(IFileSource source)
    {
        string pathExpression;
        if (source.FilePaths.Count > 1)
        {
            // Multiple files: ['path1', 'path2', ...]
            var escaped = source.FilePaths.Select(p => $"'{p.Replace("'", "''")}'");
            pathExpression = $"[{string.Join(", ", escaped)}]";
        }
        else
        {
            // Single file: 'path'
            pathExpression = $"'{source.FilePath.Replace("'", "''")}'";
        }

        return source.GenerateReadFunction(pathExpression);
    }

}
