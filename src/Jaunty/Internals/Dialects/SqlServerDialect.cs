using Jaunty.Internals.BulkCopy;

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

    public bool IsKeyword(string identifier) => identifier is not null && Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        var escapedTable = IsKeyword(tableName) ? $"[{tableName}]" : tableName;

        if (string.IsNullOrWhiteSpace(schemaName))
            return escapedTable;

        var escapedSchema = (schemaName is not null && IsKeyword(schemaName)) ? $"[{schemaName}]" : schemaName;
        return $"{escapedSchema}.{escapedTable}";
    }

    public string EscapeColumnName(string columnName)
    {
        return IsKeyword(columnName) ? $"[{columnName}]" : columnName;
    }

    public string GetLastInsertIdSql(params string[] columnNames)
    {
        return "SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
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

    // String functions
    public string GenerateLength(string expression) => $"LEN({expression})";
    public string GenerateUpper(string expression) => $"UPPER({expression})";
    public string GenerateLower(string expression) => $"LOWER({expression})";
    public string GenerateTrim(string expression) => $"TRIM({expression})";
    public string GenerateSubstring(string expression, string start, string length) => $"SUBSTRING({expression}, {start}, {length})";

    // Date functions - SQL Server uses YEAR(), MONTH(), DAY()
    public string GenerateYear(string expression) => $"YEAR({expression})";
    public string GenerateMonth(string expression) => $"MONTH({expression})";
    public string GenerateDay(string expression) => $"DAY({expression})";

    // Multi-row insert support
    public bool SupportsMultiRowInsert => true;
    public int MaxParametersPerStatement => 2100;

    // Upsert support - SQL Server uses MERGE
    public bool SupportsUpsert => true;

    public string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns)
    {
        // SQL Server: MERGE INTO table AS target USING (VALUES (...)) AS source (...) ON ... WHEN MATCHED THEN UPDATE WHEN NOT MATCHED THEN INSERT
        var sb = new System.Text.StringBuilder(512);
        sb.Append("MERGE INTO ");
        sb.Append(tableName);
        sb.Append(" AS target USING (VALUES (");
        
        for (int i = 0; i < insertParams.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(insertParams[i]);
        }

        sb.Append(")) AS source (");
        for (int i = 0; i < insertColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(insertColumns[i]);
        }

        sb.Append(") ON ");

        for (int i = 0; i < keyColumns.Length; i++)
        {
            if (i > 0) sb.Append(" AND ");
            sb.Append("target.");
            sb.Append(keyColumns[i]);
            sb.Append(" = source.");
            sb.Append(keyColumns[i]);
        }

        sb.Append(" WHEN MATCHED THEN UPDATE SET ");
        for (int i = 0; i < updateColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("target.");
            sb.Append(updateColumns[i]);
            sb.Append(" = source.");
            sb.Append(updateColumns[i]);
        }

        sb.Append(" WHEN NOT MATCHED THEN INSERT (");
        for (int i = 0; i < insertColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(insertColumns[i]);
        }

        sb.Append(") VALUES (");
        for (int i = 0; i < insertColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append("source.");
            sb.Append(insertColumns[i]);
        }
        sb.Append(");");

        return sb.ToString();
    }

    // Window functions - SQL Server supports standard SQL:2003 window functions
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

    // Bulk copy support - SQL Server has SqlBulkCopy
    // Implementation moved to Jaunty.Extensions.Reflection (optional package)
    public bool SupportsNativeBulkCopy => false;

    public IBulkCopyProvider? CreateBulkCopyProvider()
    {
        return null; // Requires Jaunty.Extensions.Reflection package
    }
}
