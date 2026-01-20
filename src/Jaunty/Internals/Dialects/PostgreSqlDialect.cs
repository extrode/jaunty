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
}
