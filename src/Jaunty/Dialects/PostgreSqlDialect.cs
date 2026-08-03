using Jaunty.Configuration;

namespace Jaunty.Dialects;

/// <summary>
/// PostgreSQL dialect.
/// Uses "quotes" only for SQL keywords.
/// Default schema: "public"
/// </summary>
internal sealed class PostgreSqlDialect : ISqlDialect, ISubstringToEndDialect
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

    public string ParameterPrefix => "@";

    public string GetDefaultSchema() => "public";

    public bool IsKeyword(string identifier) => identifier is not null && Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        SqlIdentifierValidator.Validate(tableName, nameof(tableName), SqlIdentifierFlavor.PostgreSql);
        var escapedTable = IsKeyword(tableName) ? $"\"{tableName}\"" : tableName;

        if (string.IsNullOrWhiteSpace(schemaName))
            return escapedTable;

        SqlIdentifierValidator.Validate(schemaName!, nameof(schemaName), SqlIdentifierFlavor.PostgreSql);
        var escapedSchema = IsKeyword(schemaName!) ? $"\"{schemaName}\"" : schemaName;
        return $"{escapedSchema}.{escapedTable}";
    }

    public string EscapeColumnName(string columnName)
    {
        SqlIdentifierValidator.Validate(columnName, nameof(columnName), SqlIdentifierFlavor.PostgreSql);
        return IsKeyword(columnName) ? $"\"{columnName}\"" : columnName;
    }

    public string EscapeStringLiteral(string value)
    {
        return value.Replace("'", "''");
    }

    public string GetLastInsertIdSql(params string[] columnNames)
    {
        // R27 batch 7: the empty-array case used to fabricate "RETURNING id", a column name it
        // was never given. lastval() is the session's most recent sequence value - the PostgreSQL
        // analog of what the other dialects return when handed no column names.
        if (columnNames.Length == 0) return "SELECT lastval();";
        return $"RETURNING {string.Join(", ", columnNames)};";
    }

    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        return $"{baseSql} LIMIT {fetchNext} OFFSET {offset}";
    }

    /// <remarks>
    /// AUD-R35-061. This used to emit <c>{column} COLLATE "C" LIKE {parameter}</c>, "to be explicit
    /// and ensure consistency". The clause was doing nothing and costing an index: PostgreSQL's
    /// <c>LIKE</c> is not collation-driven for case - it compares characters directly, so it is
    /// case-sensitive under every collation, <c>"C"</c> included - while wrapping the column in a
    /// <c>COLLATE</c> expression makes it a non-simple operand that an ordinary btree index cannot
    /// be seeked on, so even a <c>StartsWith</c> pattern (the one LIKE shape that can seek) forced a
    /// scan. <see cref="SqlServerDialect.GenerateCaseSensitiveLike"/> was fixed for exactly this and
    /// its siblings were left alone; MySQL's half went with AUD-R35-017.
    /// <para>
    /// <see cref="GenerateCaseInsensitiveEquals"/> keeps <c>LOWER(column)</c> deliberately. There the
    /// wrapping is what produces the semantics, not a redundant assertion of them, and PostgreSQL
    /// offers no parameter-side equivalent that is available on every installation - a functional
    /// index on <c>LOWER(column)</c> is the answer, and that is the schema's call, not the driver's.
    /// </para>
    /// </remarks>
    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        return $"{columnName} LIKE {parameterName} ESCAPE '{EscapeStringLiteral(escapeChar)}'";
    }

    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // PostgreSQL: Use ILIKE for case-insensitive matching
        // ILIKE is PostgreSQL-specific and very efficient
        // Alternative: Use UPPER() or LOWER() but ILIKE is preferred
        return $"{columnName} ILIKE {parameterName} ESCAPE '{EscapeStringLiteral(escapeChar)}'";
    }

    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        // PostgreSQL: = is case-sensitive by default
        // Use LOWER() for case-insensitive comparison
        return $"LOWER({columnName}) = LOWER({parameterName})";
    }

    public string FormatContainsPattern(string value) => $"%{EscapeLikeWildcards(value)}%";
    public string FormatStartsWithPattern(string value) => $"{EscapeLikeWildcards(value)}%";
    public string FormatEndsWithPattern(string value) => $"%{EscapeLikeWildcards(value)}";

    public string FormatBooleanLiteral(bool value) => value ? "TRUE" : "FALSE";

    // GenerateCaseSensitiveLike/GenerateCaseInsensitiveLike declare ESCAPE '\', so literal
    // occurrences of the escape char and LIKE wildcard chars (%, _) must be escaped in the
    // value or they change query semantics instead of matching literally.
    private static string EscapeLikeWildcards(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");

    // PostgreSQL: session_replication_role = 'replica' disables all triggers including FK constraints
    // This is a session-level setting that affects all tables
    public string? GetDisableForeignKeyChecksSql() => "SET session_replication_role = 'replica'";

    public string? GetEnableForeignKeyChecksSql() => "SET session_replication_role = 'origin'";

    public bool SupportsForeignKeyToggle => true;

    public bool RequiresAutocommitForForeignKeyToggle => false;

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

    /// <summary>Omitting FOR is the ANSI SQL form for "to the end", and needs no sentinel length.</summary>
    public string GenerateSubstringToEnd(string expression, string start)
        => $"SUBSTRING({expression} FROM {start})";

    // Date functions - PostgreSQL uses EXTRACT
    public string GenerateYear(string expression) => $"EXTRACT(YEAR FROM {expression})";
    public string GenerateMonth(string expression) => $"EXTRACT(MONTH FROM {expression})";
    public string GenerateDay(string expression) => $"EXTRACT(DAY FROM {expression})";

    // Multi-row insert support
    public bool SupportsMultiRowInsert => true;
    public int MaxParametersPerStatement => 65535;

    // Upsert support - PostgreSQL uses ON CONFLICT (like SQLite)
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
        // PostgreSQL: INSERT INTO table (...) VALUES (...) ON CONFLICT (key) DO UPDATE SET col = EXCLUDED.col
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

        // A key-only entity (no non-key updatable columns) has nothing to set on conflict; DO
        // NOTHING is the standard idiom instead of emitting "DO UPDATE SET " with nothing after it.
        if (updateColumns.Length == 0)
        {
            sb.Append(") DO NOTHING");
            return sb.ToString();
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

    // Bulk copy support - PostgreSQL has COPY command (binary and text formats)
    // Implementation moved to Jaunty.Extensions.Reflection (optional package)
    public bool SupportsNativeBulkCopy => false;

    public IBulkCopyProvider? CreateBulkCopyProvider()
    {
        return null; // Requires Jaunty.Extensions.Reflection package
    }
}