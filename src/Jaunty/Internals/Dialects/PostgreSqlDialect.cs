namespace Jaunty.Internals.Dialects;

/// <summary>
/// PostgreSQL dialect.
/// Uses "quotes" only for SQL keywords.
/// Default schema: "public"
/// </summary>
internal sealed class PostgreSqlDialect : ISqlDialect
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL", "ANALYSE", "ANALYZE", "AND", "ANY", "ARRAY", "AS", "ASC", "ASYMMETRIC",
        "AUTHORIZATION", "BETWEEN", "BIGINT", "BINARY", "BIT", "BOOLEAN", "BOTH", "CASE",
        "CAST", "CHAR", "CHARACTER", "CHECK", "COALESCE", "COLLATE", "COLLATION", "COLUMN",
        "CONCURRENTLY", "CONSTRAINT", "CREATE", "CROSS", "CURRENT_CATALOG", "CURRENT_DATE",
        "CURRENT_ROLE", "CURRENT_SCHEMA", "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_USER",
        "DEC", "DECIMAL", "DEFAULT", "DEFERRABLE", "DESC", "DISTINCT", "DO", "ELSE", "END",
        "EXCEPT", "EXISTS", "EXTRACT", "FALSE", "FETCH", "FLOAT", "FOR", "FOREIGN", "FREEZE",
        "FROM", "FULL", "GRANT", "GROUP", "HAVING", "ILIKE", "IN", "INITIALLY", "INNER",
        "INOUT", "INT", "INTEGER", "INTERSECT", "INTERVAL", "INTO", "IS", "ISNULL", "JOIN",
        "LATERAL", "LEADING", "LEFT", "LIKE", "LIMIT", "LOCALTIME", "LOCALTIMESTAMP", "NATIONAL",
        "NATURAL", "NCHAR", "NONE", "NOT", "NOTNULL", "NULL", "NULLIF", "NUMERIC", "OFFSET",
        "ON", "ONLY", "OR", "ORDER", "OUT", "OUTER", "OVERLAPS", "OVERLAY", "PLACING", "POSITION",
        "PRECISION", "PRIMARY", "REAL", "REFERENCES", "RETURNING", "RIGHT", "ROW", "SELECT",
        "SESSION_USER", "SETOF", "SIMILAR", "SMALLINT", "SOME", "SUBSTRING", "SYMMETRIC",
        "TABLE", "TABLESAMPLE", "THEN", "TIME", "TIMESTAMP", "TO", "TRAILING", "TREAT", "TRIM",
        "TRUE", "UNION", "UNIQUE", "USER", "USING", "VALUES", "VARCHAR", "VARIADIC", "VERBOSE",
        "WHEN", "WHERE", "WINDOW", "WITH", "ORDER", "USER"
    };

    public string GetDefaultSchema() => "public";

    public bool IsKeyword(string identifier) => Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        var escapedTable = IsKeyword(tableName) ? $"\"{tableName}\"" : tableName;

        if (string.IsNullOrWhiteSpace(schemaName))
            return escapedTable;

        var escapedSchema = IsKeyword(schemaName) ? $"\"{schemaName}\"" : schemaName;
        return $"{escapedSchema}.{escapedTable}";
    }

    public string EscapeColumnName(string columnName)
    {
        return IsKeyword(columnName) ? $"\"{columnName}\"" : columnName;
    }

    public string GetLastInsertIdSql()
    {
        return "RETURNING id;";
    }

    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        return $"{baseSql} LIMIT {fetchNext} OFFSET {offset}";
    }

    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // PostgreSQL: Standard LIKE is case-sensitive by default
        // However, to be explicit and ensure consistency, we use COLLATE "C"
        // "C" collation provides byte-by-byte comparison (case-sensitive)
        // This is bulletproof and matches C# string.Contains() behavior
        return $"{columnName} COLLATE \"C\" LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // PostgreSQL: Use ILIKE for case-insensitive matching
        // ILIKE is PostgreSQL-specific and very efficient
        // Alternative: Use UPPER() or LOWER() but ILIKE is preferred
        return $"{columnName} ILIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        // PostgreSQL: = is case-sensitive by default
        // Use LOWER() for case-insensitive comparison
        return $"LOWER({columnName}) = LOWER({parameterName})";
    }

    public string FormatContainsPattern(string value) => $"%{value}%";
    public string FormatStartsWithPattern(string value) => $"{value}%";
    public string FormatEndsWithPattern(string value) => $"%{value}";

    // PostgreSQL: session_replication_role = 'replica' disables all triggers including FK constraints
    // This is a session-level setting that affects all tables
    public string? GetDisableForeignKeyChecksSql() => "SET session_replication_role = 'replica'";

    public string? GetEnableForeignKeyChecksSql() => "SET session_replication_role = 'origin'";

    public bool SupportsForeignKeyToggle => true;

    public string GenerateCoalesce(params string[] expressions)
    {
        return $"COALESCE({string.Join(", ", expressions)})";
    }

    public string GenerateIsNull(string expression, string defaultExpression)
    {
        // PostgreSQL doesn't have ISNULL, use COALESCE instead
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
    public string GenerateSubstring(string expression, string start, string length) => $"SUBSTRING({expression} FROM {start} FOR {length})";

    // Date functions - PostgreSQL uses EXTRACT
    public string GenerateYear(string expression) => $"EXTRACT(YEAR FROM {expression})";
    public string GenerateMonth(string expression) => $"EXTRACT(MONTH FROM {expression})";
    public string GenerateDay(string expression) => $"EXTRACT(DAY FROM {expression})";

    // Upsert support - PostgreSQL uses ON CONFLICT (like SQLite)
    public bool SupportsUpsert => true;

    public string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns)
    {
        // PostgreSQL: INSERT INTO table (...) VALUES (...) ON CONFLICT (key) DO UPDATE SET col = EXCLUDED.col
        var sb = new System.Text.StringBuilder(256);
        sb.Append("INSERT INTO ");
        sb.Append(tableName);
        sb.Append(" (");
        sb.Append(string.Join(", ", insertColumns));
        sb.Append(") VALUES (");
        sb.Append(string.Join(", ", insertParams));
        sb.Append(") ON CONFLICT (");
        sb.Append(string.Join(", ", keyColumns));
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

    // Window functions - PostgreSQL supports standard SQL:2003 window functions
    public string GenerateRowNumber() => "ROW_NUMBER()";
    public string GenerateRank() => "RANK()";
    public string GenerateDenseRank() => "DENSE_RANK()";
    public string GenerateNTile(int buckets) => $"NTILE({buckets})";

    public string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy)
    {
        var sb = new System.Text.StringBuilder(64);
        sb.Append(" OVER (");

        if (partitionBy is { Length: > 0 })
        {
            sb.Append("PARTITION BY ");
            sb.Append(string.Join(", ", partitionBy));
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
}
