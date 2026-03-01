namespace Jaunty.Internals.Dialects;

/// <summary>
/// SQLite dialect.
/// Uses "quotes" only for SQL keywords.
/// Default schema: null (SQLite doesn't support schemas)
/// </summary>
internal sealed class SQLiteDialect : ISqlDialect
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ABORT", "ACTION", "ADD", "AFTER", "ALL", "ALTER", "ANALYZE", "AND", "AS", "ASC",
        "ATTACH", "AUTOINCREMENT", "BEFORE", "BEGIN", "BETWEEN", "BY", "CASCADE", "CASE",
        "CAST", "CHECK", "COLLATE", "COLUMN", "COMMIT", "CONFLICT", "CONSTRAINT", "CREATE",
        "CROSS", "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "DATABASE", "DEFAULT",
        "DEFERRABLE", "DEFERRED", "DELETE", "DESC", "DETACH", "DISTINCT", "DROP", "EACH",
        "ELSE", "END", "ESCAPE", "EXCEPT", "EXCLUSIVE", "EXISTS", "EXPLAIN", "FAIL", "FOR",
        "FOREIGN", "FROM", "FULL", "GLOB", "GROUP", "HAVING", "IF", "IGNORE", "IMMEDIATE",
        "IN", "INDEX", "INDEXED", "INITIALLY", "INNER", "INSERT", "INSTEAD", "INTERSECT",
        "INTO", "IS", "ISNULL", "JOIN", "KEY", "LEFT", "LIKE", "LIMIT", "MATCH", "NATURAL",
        "NO", "NOT", "NOTNULL", "NULL", "OF", "OFFSET", "ON", "OR", "ORDER", "OUTER", "PLAN",
        "PRAGMA", "PRIMARY", "QUERY", "RAISE", "RECURSIVE", "REFERENCES", "REGEXP", "REINDEX",
        "RELEASE", "RENAME", "REPLACE", "RESTRICT", "RIGHT", "ROLLBACK", "ROW", "SAVEPOINT",
        "SELECT", "SET", "TABLE", "TEMP", "TEMPORARY", "THEN", "TO", "TRANSACTION", "TRIGGER",
        "UNION", "UNIQUE", "UPDATE", "USING", "VACUUM", "VALUES", "VIEW", "VIRTUAL", "WHEN",
        "WHERE", "WITH", "WITHOUT", "ORDER", "USER"
    };

    public string GetDefaultSchema() => string.Empty; // SQLite doesn't support schemas

    public bool IsKeyword(string identifier) => Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        return IsKeyword(tableName) ? $"\"{tableName}\"" : tableName;
    }

    public string EscapeColumnName(string columnName)
    {
        return IsKeyword(columnName) ? $"\"{columnName}\"" : columnName;
    }

    public string GetLastInsertIdSql(params string[] columnNames)
    {
        return "SELECT last_insert_rowid();";
    }

    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        return $"{baseSql} LIMIT {fetchNext} OFFSET {offset}";
    }

    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // SQLite Challenge: LIKE is case-insensitive, GLOB is case-sensitive but has different syntax
        //
        // Approach: Use UPPER() comparison trick
        // If UPPER(column) != column OR UPPER(column) != UPPER(param), then they differ in case
        // We combine this with LIKE for pattern matching
        //
        // Actually, simpler approach: Check if the column exactly matches the pattern after LIKE
        // Use: column LIKE pattern AND column = REPLACE(REPLACE(pattern, '%', ...), '_', ...)
        //
        // Most practical: Just use LIKE and accept case-insensitivity for SQLite
        // OR use GLOB with proper wildcard conversion
        //
        // Let's use GLOB with escaped special characters
        // We need to escape: [, ], -, \, then convert % to * and _ to ?

        // GLOB pattern conversion happens in the parameter value
        // The parameter should have LIKE wildcards (%, _) which we convert to GLOB (*, ?)
        // But we also need to escape GLOB special chars: [, ], -, \
        //
        // This is done in WhereExpressionVisitor
        return $"{columnName} GLOB {parameterName}";
    }

    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // SQLite: Default LIKE is already case-insensitive for ASCII
        // This is the standard SQLite behavior
        return $"{columnName} LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        // SQLite: = is case-sensitive by default
        // Use LOWER() for case-insensitive comparison
        return $"LOWER({columnName}) = LOWER({parameterName})";
    }

    public string FormatContainsPattern(string value)
    {
        // SQLite GLOB uses * instead of %
        return $"*{EscapeGlobPattern(value)}*";
    }

    public string FormatStartsWithPattern(string value)
    {
        return $"{EscapeGlobPattern(value)}*";
    }

    public string FormatEndsWithPattern(string value)
    {
        return $"*{EscapeGlobPattern(value)}";
    }

    private static string EscapeGlobPattern(string value)
    {
        // Escape GLOB special characters: *, ?, [
        return value
            .Replace("[", "[[]")
            .Replace("*", "[*]")
            .Replace("?", "[?]");
    }

    public string? GetDisableForeignKeyChecksSql() => "PRAGMA foreign_keys = OFF";

    public string? GetEnableForeignKeyChecksSql() => "PRAGMA foreign_keys = ON";

    public bool SupportsForeignKeyToggle => true;

    public string GenerateCoalesce(params string[] expressions)
    {
        return $"COALESCE({string.Join(", ", expressions)})";
    }

    public string GenerateIsNull(string expression, string defaultExpression)
    {
        // SQLite uses IFNULL for the IsNull function
        return $"IFNULL({expression}, {defaultExpression})";
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
    public string GenerateSubstring(string expression, string start, string length) => $"SUBSTR({expression}, {start}, {length})";

    // Date functions - SQLite uses strftime with CAST for integer comparison
    public string GenerateYear(string expression) => $"CAST(strftime('%Y', {expression}) AS INTEGER)";
    public string GenerateMonth(string expression) => $"CAST(strftime('%m', {expression}) AS INTEGER)";
    public string GenerateDay(string expression) => $"CAST(strftime('%d', {expression}) AS INTEGER)";

    // Multi-row insert support
    public bool SupportsMultiRowInsert => true;
    public int MaxParametersPerStatement => 999;

    // Upsert support - SQLite 3.24+ supports ON CONFLICT
    public bool SupportsUpsert => true;

    public string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns)
    {
        // SQLite: INSERT INTO table (...) VALUES (...) ON CONFLICT (key) DO UPDATE SET col = excluded.col
        var sb = new System.Text.StringBuilder(256);
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
            sb.Append(" = excluded.");
            sb.Append(updateColumns[i]);
        }

        return sb.ToString();
    }

    // Window functions - SQLite 3.25+ supports standard SQL:2003 window functions
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
}
