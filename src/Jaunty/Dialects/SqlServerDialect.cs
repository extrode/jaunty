using Jaunty.Configuration;

namespace Jaunty.Dialects;

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

    public string ParameterPrefix => "@";

    public string GetDefaultSchema() => "dbo";

    public bool IsKeyword(string identifier) => identifier is not null && Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        SqlIdentifierValidator.Validate(tableName, nameof(tableName));
        var escapedTable = IsKeyword(tableName) ? $"[{tableName}]" : tableName;

        if (string.IsNullOrWhiteSpace(schemaName))
            return escapedTable;

        SqlIdentifierValidator.Validate(schemaName!, nameof(schemaName));
        var escapedSchema = IsKeyword(schemaName!) ? $"[{schemaName}]" : schemaName;
        return $"{escapedSchema}.{escapedTable}";
    }

    public string EscapeColumnName(string columnName)
    {
        SqlIdentifierValidator.Validate(columnName, nameof(columnName));
        return IsKeyword(columnName) ? $"[{columnName}]" : columnName;
    }

    public string EscapeStringLiteral(string value)
    {
        return value.Replace("'", "''");
    }

    public string GetLastInsertIdSql(params string[] columnNames)
    {
        return "SELECT CAST(SCOPE_IDENTITY() AS BIGINT);";
    }

    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        // SQL Server's OFFSET/FETCH is only valid immediately after an ORDER BY. Without one,
        // it fails at execution with "Incorrect syntax near 'OFFSET'." - unlike Postgres/MySQL/
        // SQLite, which tolerate LIMIT/OFFSET with no explicit order. Auto-inject SQL Server's
        // own idiom for "no real order needed" when the query has no top-level ORDER BY, so
        // paging behaves the same way it does on the other 3 dialects.
        string sql = HasTopLevelOrderBy(baseSql) ? baseSql : $"{baseSql} ORDER BY (SELECT NULL)";
        return $"{sql} OFFSET {offset} ROWS FETCH NEXT {fetchNext} ROWS ONLY";
    }

    // Tracks paren depth so an ORDER BY nested inside a window function's OVER(...) clause
    // (or a subquery) isn't mistaken for the query's own top-level ORDER BY. Also skips
    // string literals, quoted identifiers, and comments so "ORDER BY" text inside them
    // (or a paren inside them) can't produce a false positive/negative.
    private static bool HasTopLevelOrderBy(string sql)
    {
        int depth = 0;
        int len = sql.Length;
        int i = 0;

        while (i < len)
        {
            char c = sql[i];

            if (c == '-' && i + 1 < len && sql[i + 1] == '-')
            {
                i += 2;
                while (i < len && sql[i] is not ('\n' or '\r')) i++;
                continue;
            }

            if (c == '/' && i + 1 < len && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < len && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                i = i + 1 < len ? i + 2 : len;
                continue;
            }

            if (c is '\'' or '"' or '[')
            {
                char terminator = c == '[' ? ']' : c;
                i++;
                while (i < len)
                {
                    if (sql[i] == terminator)
                    {
                        if (i + 1 < len && sql[i + 1] == terminator) { i += 2; continue; }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            if (c == '(')
            {
                depth++;
                i++;
                continue;
            }

            if (c == ')')
            {
                depth--;
                i++;
                continue;
            }

            if (depth == 0 && i + 8 <= len &&
                string.Compare(sql, i, "ORDER BY", 0, 8, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }

            i++;
        }

        return false;
    }

    /// <remarks>
    /// The COLLATE clause is attached to the parameter, not the column, on purpose: SQL Server's
    /// collation-coercion rules give an explicit COLLATE the same precedence regardless of which
    /// operand carries it, so <c>col LIKE @p COLLATE X</c> compares identically to
    /// <c>col COLLATE X LIKE @p</c>. Attaching it to the parameter instead of the column avoids
    /// wrapping the column in an expression, which is what disqualifies an index from a seek.
    /// This lets SQL Server still seek the column's own index (e.g. for StartsWith/EndsWith or
    /// non-wildcard prefixes) and apply the explicit-collation comparison as a residual predicate,
    /// instead of forcing a full scan. A leading-wildcard Contains() still can't seek regardless of
    /// COLLATE placement, since LIKE '%...' is never seekable.
    /// </remarks>
    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // SQL Server: Use COLLATE with a case-sensitive, accent-sensitive collation
        // Latin1_General_CS_AS is widely supported and provides:
        // - CS = Case Sensitive
        // - AS = Accent Sensitive
        // This matches C# string comparison behavior
        return $"{columnName} LIKE {parameterName} COLLATE Latin1_General_CS_AS ESCAPE '{escapeChar}'";
    }

    /// <inheritdoc cref="GenerateCaseSensitiveLike"/>
    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // SQL Server: Use COLLATE with a case-insensitive collation
        // Latin1_General_CI_AS is the default for most SQL Server installations
        // - CI = Case Insensitive
        // - AS = Accent Sensitive
        return $"{columnName} LIKE {parameterName} COLLATE Latin1_General_CI_AS ESCAPE '{escapeChar}'";
    }

    /// <inheritdoc cref="GenerateCaseSensitiveLike"/>
    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        // SQL Server: Use COLLATE with a case-insensitive collation
        return $"{columnName} = {parameterName} COLLATE Latin1_General_CI_AS";
    }

    public string FormatContainsPattern(string value) => $"%{EscapeLikeWildcards(value)}%";
    public string FormatStartsWithPattern(string value) => $"{EscapeLikeWildcards(value)}%";
    public string FormatEndsWithPattern(string value) => $"%{EscapeLikeWildcards(value)}";

    // GenerateCaseSensitiveLike/GenerateCaseInsensitiveLike declare ESCAPE '\', so literal
    // occurrences of the escape char and SQL Server's LIKE wildcard chars (%, _, [) must be
    // escaped in the value or they change query semantics instead of matching literally.
    private static string EscapeLikeWildcards(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_")
        .Replace("[", "\\[");

    // SQL Server doesn't have a simple session-level FK toggle.
    // Disabling constraints requires per-table ALTER statements.
    // For bulk operations ignoring constraints, users should use NOCHECK per table
    // or handle this at the application level.
    public string? GetDisableForeignKeyChecksSql() => null;

    public string? GetEnableForeignKeyChecksSql() => null;

    public bool SupportsForeignKeyToggle => false;

    public bool RequiresAutocommitForForeignKeyToggle => false;

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
        string[] keyColumns,
        string[] keyParams)
    {
        // SQL Server: MERGE INTO table AS target USING (VALUES (...)) AS source (...) ON ... WHEN MATCHED THEN UPDATE WHEN NOT MATCHED THEN INSERT
        //
        // The USING/AS source column list must carry every key column, even an identity key
        // excluded from insertColumns (identity columns can't appear in the actual INSERT
        // list below), otherwise the ON clause's "source.<key>" reference doesn't exist.
        int extraKeyCount = 0;
        for (int i = 0; i < keyColumns.Length; i++)
        {
            if (Array.IndexOf(insertColumns, keyColumns[i]) < 0)
                extraKeyCount++;
        }

        var sourceColumns = new string[insertColumns.Length + extraKeyCount];
        var sourceParams = new string[insertParams.Length + extraKeyCount];
        Array.Copy(insertColumns, sourceColumns, insertColumns.Length);
        Array.Copy(insertParams, sourceParams, insertParams.Length);

        int sourceIndex = insertColumns.Length;
        for (int i = 0; i < keyColumns.Length; i++)
        {
            if (Array.IndexOf(insertColumns, keyColumns[i]) < 0)
            {
                sourceColumns[sourceIndex] = keyColumns[i];
                sourceParams[sourceIndex] = keyParams[i];
                sourceIndex++;
            }
        }

        var sb = new System.Text.StringBuilder(512);
        sb.Append("MERGE INTO ");
        sb.Append(tableName);
        // WITH (HOLDLOCK) escalates to a serializable-range lock on the target table for the
        // duration of the MERGE, closing the well-known race where two concurrent MERGE
        // statements both evaluate WHEN NOT MATCHED as true for the same key and both attempt
        // to INSERT, causing a duplicate-key violation.
        sb.Append(" WITH (HOLDLOCK) AS target USING (VALUES (");

        for (int i = 0; i < sourceParams.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(sourceParams[i]);
        }

        sb.Append(")) AS source (");
        for (int i = 0; i < sourceColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(sourceColumns[i]);
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

        // MERGE requires at least one WHEN clause but not specifically WHEN MATCHED; when the
        // entity has no non-key updatable columns (e.g. a key-only or all-key entity), omit the
        // clause entirely instead of emitting "WHEN MATCHED THEN UPDATE SET " with nothing after it.
        if (updateColumns.Length > 0)
        {
            sb.Append(" WHEN MATCHED THEN UPDATE SET ");
            for (int i = 0; i < updateColumns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("target.");
                sb.Append(updateColumns[i]);
                sb.Append(" = source.");
                sb.Append(updateColumns[i]);
            }
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