using Jaunty.Configuration;

namespace Jaunty.Dialects;

/// <summary>
/// SQLite dialect.
/// Uses "quotes" only for SQL keywords.
/// Default schema: null (SQLite doesn't support schemas)
/// </summary>
internal sealed class SQLiteDialect : ISqlDialect, ISubstringToEndDialect, IDecimalBindingDialect
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

    public string ParameterPrefix => "@";

    public string GetDefaultSchema() => string.Empty; // SQLite doesn't support schemas

    public bool IsKeyword(string identifier) => identifier is not null && Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        SqlIdentifierValidator.Validate(tableName, nameof(tableName));
        return IsKeyword(tableName) ? $"\"{tableName}\"" : tableName;
    }

    public string EscapeColumnName(string columnName)
    {
        SqlIdentifierValidator.Validate(columnName, nameof(columnName));
        return IsKeyword(columnName) ? $"\"{columnName}\"" : columnName;
    }

    public string EscapeStringLiteral(string value)
    {
        return value.Replace("'", "''");
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
        // GLOB, not LIKE - AUD-R25.
        //
        // This used to emit "col LIKE @p ESCAPE '\'", which is true to SQLite (LIKE is
        // case-insensitive for ASCII) but wrong for this dialect, because the pattern it is handed
        // is not a LIKE pattern. FormatContainsPattern/FormatStartsWithPattern/FormatEndsWithPattern
        // below all produce *GLOB* patterns ("*value*", escaping via [[]/[*]/[?]) to pair with
        // GenerateCaseSensitiveLike's GLOB. Feeding a GLOB pattern to LIKE gives "col LIKE '*abc*'",
        // where * is a literal - it matches nothing, and the caller's own % and _ are left
        // unescaped. Verified against SQLite: the LIKE pairing returns 0 rows where GLOB returns 1.
        //
        // Folding both sides keeps the pattern in GLOB syntax while making the comparison
        // case-insensitive. LOWER() leaves *, ? and [ untouched, so the wildcards and the
        // EscapeGlobPattern escapes survive. Like LIKE's built-in folding - and like every other
        // dialect's case-insensitive form here - this is ASCII-only, so the coverage is unchanged.
        //
        // escapeChar is unused: GLOB has no ESCAPE clause. EscapeGlobPattern handles it in the value.
        return $"LOWER({columnName}) GLOB LOWER({parameterName})";
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

    public string FormatBooleanLiteral(bool value) => value ? "1" : "0";

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

    public bool RequiresAutocommitForForeignKeyToggle => true;

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

    /// <summary>
    /// SQLite's two-argument SUBSTR returns the remainder and needs no sentinel length. Measured:
    /// against a 10,000-character value <c>SUBSTR(s, 2, 8000)</c> returns 8,000 characters while
    /// <c>SUBSTR(s, 2)</c> returns 9,999. SQLite does not accept the ANSI <c>FROM</c> form.
    /// </summary>
    public string GenerateSubstringToEnd(string expression, string start)
        => $"SUBSTR({expression}, {start})";

    // Date functions - SQLite uses strftime with CAST for integer comparison
    public string GenerateYear(string expression) => $"CAST(strftime('%Y', {expression}) AS INTEGER)";
    public string GenerateMonth(string expression) => $"CAST(strftime('%m', {expression}) AS INTEGER)";
    public string GenerateDay(string expression) => $"CAST(strftime('%d', {expression}) AS INTEGER)";

    // Multi-row insert support
    public bool SupportsMultiRowInsert => true;
    /// <summary>
    /// SQLite's <c>SQLITE_MAX_VARIABLE_NUMBER</c>, 32,766 on every build Jaunty ships against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 4, medium/bug). This was 999 - the pre-3.32 (2020) default - which understated
    /// the real limit by a factor of 33. Because
    /// <see cref="Internals.Parameters.ParameterBinder"/> enforces this value as a hard rejection
    /// rather than only as a batch-sizing hint, Jaunty refused <c>IN</c>-clause expansions the
    /// provider ran without complaint: a 1,000-id <c>WHERE Id IN @Ids</c> failed with advice to
    /// batch, on a statement the engine would have executed.
    /// </para>
    /// <para>
    /// Measured against this repo's own package graph - Microsoft.Data.Sqlite 10.0.3 over
    /// SQLitePCLRaw.bundle_e_sqlite3 3.0.3, sqlite 3.50.4 - by binding N parameters to a real
    /// <c>IN</c> list and executing it. 999, 1,000, 1,500, 5,000, 20,000 and 32,766 all succeed;
    /// 32,767 and above fail with <c>SQLite Error 1: 'too many SQL variables'</c>. The boundary is
    /// exactly 32,766, so that is the value, not a round number near it.
    /// </para>
    /// <para>
    /// The other three dialects were already correct (SQL Server 2,100, PostgreSQL and MySQL
    /// 65,535); SQLite was the outlier, and it is this repo's default development database. The
    /// value is a property of the *native* build, so a caller who supplies an older sqlite than any
    /// current package ships will now get the provider's "too many SQL variables" instead of
    /// Jaunty's message. That trade is deliberate: refusing valid queries for everyone is worse than
    /// a less friendly error for a pre-2020 build.
    /// </para>
    /// </remarks>
    public int MaxParametersPerStatement => 32766;

    // Upsert support - SQLite 3.24+ supports ON CONFLICT
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

    /// <summary>
    /// Binds a <see cref="decimal"/> as a <see cref="double"/>. Both SQLite providers - measured on
    /// System.Data.SQLite 1.0.119 and Microsoft.Data.Sqlite 10.0.3 - bind a <see cref="decimal"/> as
    /// TEXT, which SQLite converts only where there is affinity to apply it: against a column with
    /// numeric affinity it works, so <c>WHERE price = @p</c> matches, while against a bare
    /// expression <c>HAVING SUM(price) &gt; @p</c> silently matches nothing. See
    /// <see cref="IDecimalBindingDialect"/> for the measurements and for the narrower exceptions on
    /// both sides of that rule.
    /// </summary>
    /// <remarks>
    /// The cost is real and one-directional: a value past <see cref="double"/>'s 15-17 significant
    /// digits is no longer bound exactly, so an exact-INTEGER comparison past 2^53 that used to
    /// match now does not - <c>(double)9007199254740993m</c> is 9007199254740992. That is the
    /// narrower failure of the two. Without the conversion, every comparison of a
    /// <see cref="decimal"/> against any computed expression is decided by operand type rather than
    /// by value, whatever the magnitude.
    /// </remarks>
    public object ConvertDecimalParameter(decimal value) => (double)value;

    // Bulk copy support - SQLite has no native bulk copy API
    // We provide an optimized path using transactions and prepared statements
    // Implementation moved to Jaunty.Extensions.Reflection (optional package)
    public bool SupportsNativeBulkCopy => false;

    public IBulkCopyProvider? CreateBulkCopyProvider()
    {
        return null; // Requires Jaunty.Extensions.Reflection package
    }
}