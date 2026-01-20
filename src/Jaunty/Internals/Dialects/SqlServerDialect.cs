namespace Jaunty.Internals.Dialects;

/// <summary>
/// SQL Server dialect (T-SQL).
/// Uses [brackets] only for SQL keywords.
/// Default schema: "dbo"
/// </summary>
internal sealed class SqlServerDialect : ISqlDialect
{
    // Common SQL Server reserved keywords
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADD", "ALL", "ALTER", "AND", "ANY", "AS", "ASC", "AUTHORIZATION", "BACKUP",
        "BEGIN", "BETWEEN", "BREAK", "BROWSE", "BULK", "BY", "CASCADE", "CASE", "CHECK",
        "CHECKPOINT", "CLOSE", "CLUSTERED", "COALESCE", "COLLATE", "COLUMN", "COMMIT",
        "COMPUTE", "CONSTRAINT", "CONTAINS", "CONTAINSTABLE", "CONTINUE", "CONVERT",
        "CREATE", "CROSS", "CURRENT", "CURRENT_DATE", "CURRENT_TIME", "CURRENT_TIMESTAMP",
        "CURRENT_USER", "CURSOR", "DATABASE", "DBCC", "DEALLOCATE", "DECLARE", "DEFAULT",
        "DELETE", "DENY", "DESC", "DISK", "DISTINCT", "DISTRIBUTED", "DOUBLE", "DROP",
        "DUMP", "ELSE", "END", "ERRLVL", "ESCAPE", "EXCEPT", "EXEC", "EXECUTE", "EXISTS",
        "EXIT", "EXTERNAL", "FETCH", "FILE", "FILLFACTOR", "FOR", "FOREIGN", "FREETEXT",
        "FREETEXTTABLE", "FROM", "FULL", "FUNCTION", "GOTO", "GRANT", "GROUP", "HAVING",
        "HOLDLOCK", "IDENTITY", "IDENTITY_INSERT", "IDENTITYCOL", "IF", "IN", "INDEX",
        "INNER", "INSERT", "INTERSECT", "INTO", "IS", "JOIN", "KEY", "KILL", "LEFT",
        "LIKE", "LINENO", "LOAD", "MERGE", "NATIONAL", "NOCHECK", "NONCLUSTERED", "NOT",
        "NULL", "NULLIF", "OF", "OFF", "OFFSETS", "ON", "OPEN", "OPENDATASOURCE",
        "OPENQUERY", "OPENROWSET", "OPENXML", "OPTION", "OR", "ORDER", "OUTER", "OVER",
        "PERCENT", "PIVOT", "PLAN", "PRECISION", "PRIMARY", "PRINT", "PROC", "PROCEDURE",
        "PUBLIC", "RAISERROR", "READ", "READTEXT", "RECONFIGURE", "REFERENCES", "REPLICATION",
        "RESTORE", "RESTRICT", "RETURN", "REVERT", "REVOKE", "RIGHT", "ROLLBACK", "ROWCOUNT",
        "ROWGUIDCOL", "RULE", "SAVE", "SCHEMA", "SECURITYAUDIT", "SELECT", "SEMANTICKEYPHRASETABLE",
        "SEMANTICSIMILARITYDETAILSTABLE", "SEMANTICSIMILARITYTABLE", "SESSION_USER", "SET",
        "SETUSER", "SHUTDOWN", "SOME", "STATISTICS", "SYSTEM_USER", "TABLE", "TABLESAMPLE",
        "TEXTSIZE", "THEN", "TO", "TOP", "TRAN", "TRANSACTION", "TRIGGER", "TRUNCATE",
        "TRY_CONVERT", "TSEQUAL", "UNION", "UNIQUE", "UNPIVOT", "UPDATE", "UPDATETEXT",
        "USE", "USER", "VALUES", "VARYING", "VIEW", "WAITFOR", "WHEN", "WHERE", "WHILE",
        "WITH", "WITHIN GROUP", "WRITETEXT", "ORDER", "USER"
    };

    public string GetDefaultSchema() => "dbo";

    public bool IsKeyword(string identifier) => Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        var escapedTable = IsKeyword(tableName) ? $"[{tableName}]" : tableName;

        if (string.IsNullOrWhiteSpace(schemaName))
            return escapedTable;

        var escapedSchema = IsKeyword(schemaName) ? $"[{schemaName}]" : schemaName;
        return $"{escapedSchema}.{escapedTable}";
    }

    public string EscapeColumnName(string columnName)
    {
        return IsKeyword(columnName) ? $"[{columnName}]" : columnName;
    }

    public string GetLastInsertIdSql()
    {
        return "SELECT CAST(SCOPE_IDENTITY() AS INT);";
    }

    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        return $"{baseSql} OFFSET {offset} ROWS FETCH NEXT {fetchNext} ROWS ONLY";
    }

    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // SQL Server: Use COLLATE with a case-sensitive, accent-sensitive collation
        // Latin1_General_CS_AS is widely supported and provides:
        // - CS = Case Sensitive
        // - AS = Accent Sensitive
        // This matches C# string comparison behavior
        return $"{columnName} COLLATE Latin1_General_CS_AS LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // SQL Server: Use COLLATE with a case-insensitive collation
        // Latin1_General_CI_AS is the default for most SQL Server installations
        // - CI = Case Insensitive
        // - AS = Accent Sensitive
        return $"{columnName} COLLATE Latin1_General_CI_AS LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        // SQL Server: Use COLLATE with a case-insensitive collation
        return $"{columnName} COLLATE Latin1_General_CI_AS = {parameterName}";
    }

    public string FormatContainsPattern(string value) => $"%{value}%";
    public string FormatStartsWithPattern(string value) => $"{value}%";
    public string FormatEndsWithPattern(string value) => $"%{value}";

    // SQL Server doesn't have a simple session-level FK toggle.
    // Disabling constraints requires per-table ALTER statements.
    // For bulk operations ignoring constraints, users should use NOCHECK per table
    // or handle this at the application level.
    public string? GetDisableForeignKeyChecksSql() => null;

    public string? GetEnableForeignKeyChecksSql() => null;

    public bool SupportsForeignKeyToggle => false;

    public string GenerateCoalesce(params string[] expressions)
    {
        return $"COALESCE({string.Join(", ", expressions)})";
    }

    public string GenerateIsNull(string expression, string defaultExpression)
    {
        // SQL Server uses ISNULL for the IsNull function
        return $"ISNULL({expression}, {defaultExpression})";
    }

    public string GenerateNullIf(string expression, string compareExpression)
    {
        return $"NULLIF({expression}, {compareExpression})";
    }
}
