using System.Text;

using Jaunty.Dialects;
using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Interfaces;
using Jaunty.Configuration;

namespace Jaunty.FlatFiles.DuckDB.Dialects;

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

    /// <inheritdoc />
    public string ParameterPrefix => "$";

    /// <inheritdoc />
    public string GetDefaultSchema() => "main";

    /// <inheritdoc />
    public bool IsKeyword(string identifier) => identifier is not null && Keywords.Contains(identifier);

    /// <inheritdoc />
    /// <summary>
    /// Quotes an identifier for DuckDB, doubling any embedded double quotes so the
    /// identifier cannot break out of the quoted context (e.g. names derived from
    /// filenames or [Table]/[Column] attributes).
    /// </summary>
    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    /// <inheritdoc />
    public string EscapeTableName(string? schemaName, string tableName)
    {
        // DuckDB uses double-quote escaping like PostgreSQL
        // Always quote identifiers for safety with flat file column names
        var escapedTable = QuoteIdentifier(tableName);

        if (string.IsNullOrWhiteSpace(schemaName))
            return escapedTable;

        return $"{QuoteIdentifier(schemaName)}.{escapedTable}";
    }

    /// <inheritdoc />
    public string EscapeColumnName(string columnName)
    {
        // If already escaped (starts and ends with quotes), return as-is to prevent double-escaping.
        // This is necessary because CachedDialectMetadata pre-escapes column names,
        // and BuildSelectSql calls EscapeColumnName again on those cached values.
        if (columnName.Length >= 2 && columnName[0] == '"' && columnName[^1] == '"')
            return columnName;

        return QuoteIdentifier(columnName);
    }

    /// <inheritdoc />
    public string GetLastInsertIdSql(params string[] columnNames)
    {
        // DuckDB does not have a direct last_insert_id equivalent
        // Use RETURNING clause instead (handled by the caller)
        if (columnNames.Length == 0) return "RETURNING *;";
        return $"RETURNING {string.Join(", ", columnNames)};";
    }

    /// <inheritdoc />
    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        return $"{baseSql} LIMIT {fetchNext} OFFSET {offset}";
    }

    /// <inheritdoc />
    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // DuckDB: LIKE is case-sensitive by default (like PostgreSQL)
        return $"{columnName} LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    /// <inheritdoc />
    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // DuckDB supports ILIKE (like PostgreSQL)
        return $"{columnName} ILIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    /// <inheritdoc />
    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        return $"LOWER({columnName}) = LOWER({parameterName})";
    }

    /// <inheritdoc />
    public string FormatContainsPattern(string value) => $"%{EscapeLikeWildcards(value)}%";

    /// <inheritdoc />
    public string FormatStartsWithPattern(string value) => $"{EscapeLikeWildcards(value)}%";

    /// <inheritdoc />
    public string FormatEndsWithPattern(string value) => $"%{EscapeLikeWildcards(value)}";

    // GenerateCaseSensitiveLike/GenerateCaseInsensitiveLike declare ESCAPE '\', so literal
    // occurrences of the escape char and LIKE wildcard chars (%, _) must be escaped in the
    // value or they change query semantics instead of matching literally.
    private static string EscapeLikeWildcards(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    /// <inheritdoc />
    public string? GetDisableForeignKeyChecksSql() => null;

    /// <inheritdoc />
    public string? GetEnableForeignKeyChecksSql() => null;

    /// <inheritdoc />
    public bool SupportsForeignKeyToggle => false;

    /// <inheritdoc />
    public string GenerateCoalesce(params string[] expressions)
    {
        return $"COALESCE({string.Join(", ", expressions)})";
    }

    /// <inheritdoc />
    public string GenerateIsNull(string expression, string defaultExpression)
    {
        // DuckDB uses COALESCE (same as PostgreSQL)
        return $"COALESCE({expression}, {defaultExpression})";
    }

    /// <inheritdoc />
    public string GenerateNullIf(string expression, string compareExpression)
    {
        return $"NULLIF({expression}, {compareExpression})";
    }

    /// <inheritdoc />
    public string GenerateLength(string expression) => $"LENGTH({expression})";

    /// <inheritdoc />
    public string GenerateUpper(string expression) => $"UPPER({expression})";

    /// <inheritdoc />
    public string GenerateLower(string expression) => $"LOWER({expression})";

    /// <inheritdoc />
    public string GenerateTrim(string expression) => $"TRIM({expression})";

    /// <inheritdoc />
    public string GenerateSubstring(string expression, string start, string length) => $"SUBSTRING({expression}, {start}, {length})";

    /// <inheritdoc />
    public string GenerateYear(string expression) => $"EXTRACT(YEAR FROM {expression})";

    /// <inheritdoc />
    public string GenerateMonth(string expression) => $"EXTRACT(MONTH FROM {expression})";

    /// <inheritdoc />
    public string GenerateDay(string expression) => $"EXTRACT(DAY FROM {expression})";

    /// <inheritdoc />
    public bool SupportsMultiRowInsert => true;

    /// <inheritdoc />
    public int MaxParametersPerStatement => 32768;

    /// <inheritdoc />
    public bool SupportsUpsert => true;

    /// <inheritdoc />
    public string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns,
        string[] keyParams)
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

    /// <inheritdoc />
    public string GenerateRowNumber() => "ROW_NUMBER()";

    /// <inheritdoc />
    public string GenerateRank() => "RANK()";

    /// <inheritdoc />
    public string GenerateDenseRank() => "DENSE_RANK()";

    /// <inheritdoc />
    public string GenerateNTile(int buckets) => $"NTILE({buckets})";

    /// <inheritdoc />
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

    /// <inheritdoc />
    public string GenerateWindowAggregate(string function, string? expression)
    {
        return expression is null ? $"{function}(*)" : $"{function}({expression})";
    }

    /// <inheritdoc />
    public bool SupportsNativeBulkCopy => false;

    /// <inheritdoc />
    public IBulkCopyProvider? CreateBulkCopyProvider() => null;

    // ==========================================
    // IFlatFileDialect Implementation
    // ==========================================

    /// <inheritdoc />
    public string GenerateCreateViewSql(IFileSource source)
    {
        var readFunction = GenerateReadFunction(source);
        return $"CREATE OR REPLACE VIEW {QuoteIdentifier(source.TableName)} AS SELECT * FROM {readFunction}";
    }

    /// <inheritdoc />
    public string GenerateCreateTableAsSql(IFileSource source)
    {
        var readFunction = GenerateReadFunction(source);
        return $"CREATE OR REPLACE TABLE {QuoteIdentifier(source.TableName)} AS SELECT * FROM {readFunction}";
    }

    /// <inheritdoc />
    public string GeneratePromoteToTableSql(IFileSource source)
    {
        var tmpTable = QuoteIdentifier(source.TableName + "_tmp");
        var table = QuoteIdentifier(source.TableName);

        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE {tmpTable} AS SELECT * FROM {table}; ");
        sb.Append($"DROP VIEW {table}; ");
        sb.Append($"ALTER TABLE {tmpTable} RENAME TO {table};");
        return sb.ToString();
    }

    /// <summary>
    /// Formats that DuckDB's <c>COPY ... TO</c> statement natively supports as a write target.
    /// Table formats such as Delta Lake and Iceberg are read-only via DuckDB's scan functions
    /// and cannot be used as a COPY TO format.
    /// </summary>
    private static readonly HashSet<string> _supportedCopyToFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        FileFormats.Csv,
        FileFormats.Tsv,
        FileFormats.Parquet,
        FileFormats.Json,
        FileFormats.Excel,
    };

    /// <inheritdoc />
    public string GenerateCopyToSql(string tableName, string outputPath, string format)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (!_supportedCopyToFormats.Contains(format))
        {
            throw new NotSupportedException(
                $"Format '{format}' is not supported by DuckDB's COPY TO statement. " +
                $"Supported formats: {string.Join(", ", _supportedCopyToFormats.OrderBy(f => f))}. " +
                "Table formats such as Delta Lake and Iceberg are read-only and cannot be written via COPY TO.");
        }

        var escapedPath = outputPath.Replace("'", "''");

        // Map logical format to DuckDB format name
        var duckDbFormat = format.ToUpperInvariant() switch
        {
            "CSV" => "CSV",
            "TSV" => "CSV",
            "PARQUET" => "PARQUET",
            "JSON" => "JSON",
            "XLSX" => "XLSX",
            _ => format.ToUpperInvariant()
        };

        var sb = new StringBuilder();
        sb.Append($"COPY {QuoteIdentifier(tableName)} TO '{escapedPath}' (FORMAT {duckDbFormat}");

        if (string.Equals(format, FileFormats.Tsv, StringComparison.OrdinalIgnoreCase))
            sb.Append(", DELIMITER '\t'");

        // HEADER is only valid for CSV/TSV
        if (string.Equals(format, FileFormats.Csv, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format, FileFormats.Tsv, StringComparison.OrdinalIgnoreCase))
            sb.Append(", HEADER true");

        sb.Append(')');
        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateCopyToSql(string tableName, string outputPath, IFileSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!_supportedCopyToFormats.Contains(source.DuckDbFormatName))
        {
            throw new NotSupportedException(
                $"Format '{source.DuckDbFormatName}' (source format '{source.Format}') is not supported by DuckDB's COPY TO statement. " +
                $"Supported formats: {string.Join(", ", _supportedCopyToFormats.OrderBy(f => f))}. " +
                "Table formats such as Delta Lake and Iceberg are read-only and cannot be written via COPY TO.");
        }

        var escapedPath = outputPath.Replace("'", "''");

        var sb = new StringBuilder();
        sb.Append($"COPY {QuoteIdentifier(tableName)} TO '{escapedPath}' (FORMAT {source.DuckDbFormatName}");

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
            IEnumerable<string> escaped = source.FilePaths.Select(p => $"'{p.Replace("'", "''")}'");
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