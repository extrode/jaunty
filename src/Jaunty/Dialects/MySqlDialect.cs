using Jaunty.Internals.BulkCopy;

namespace Jaunty.Dialects;

/// <summary>
/// MySQL dialect.
/// Uses `backticks` only for SQL keywords.
/// Default schema: null (MySQL doesn't use schemas the same way)
/// </summary>
internal sealed class MySqlDialect : ISqlDialect
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACCESSIBLE", "ADD", "ALL", "ALTER", "ANALYZE", "AND", "AS", "ASC", "ASENSITIVE",
        "BEFORE", "BETWEEN", "BIGINT", "BINARY", "BLOB", "BOTH", "BY", "CALL", "CASCADE",
        "CASE", "CHANGE", "CHAR", "CHARACTER", "CHECK", "COLLATE", "COLUMN", "CONDITION",
        "CONSTRAINT", "CONTINUE", "CONVERT", "CREATE", "CROSS", "CURRENT_DATE", "CURRENT_TIME",
        "CURRENT_TIMESTAMP", "CURRENT_USER", "CURSOR", "DATABASE", "DATABASES", "DAY_HOUR",
        "DAY_MICROSECOND", "DAY_MINUTE", "DAY_SECOND", "DEC", "DECIMAL", "DECLARE", "DEFAULT",
        "DELAYED", "DELETE", "DESC", "DESCRIBE", "DETERMINISTIC", "DISTINCT", "DISTINCTROW",
        "DIV", "DOUBLE", "DROP", "DUAL", "EACH", "ELSE", "ELSEIF", "ENCLOSED", "ESCAPED",
        "EXISTS", "EXIT", "EXPLAIN", "FALSE", "FETCH", "FLOAT", "FLOAT4", "FLOAT8", "FOR",
        "FORCE", "FOREIGN", "FROM", "FULLTEXT", "GRANT", "GROUP", "HAVING", "HIGH_PRIORITY",
        "HOUR_MICROSECOND", "HOUR_MINUTE", "HOUR_SECOND", "IF", "IGNORE", "IN", "INDEX",
        "INFILE", "INNER", "INOUT", "INSENSITIVE", "INSERT", "INT", "INT1", "INT2", "INT3",
        "INT4", "INT8", "INTEGER", "INTERVAL", "INTO", "IS", "ITERATE", "JOIN", "KEY", "KEYS",
        "KILL", "LEADING", "LEAVE", "LEFT", "LIKE", "LIMIT", "LINEAR", "LINES", "LOAD",
        "LOCALTIME", "LOCALTIMESTAMP", "LOCK", "LONG", "LONGBLOB", "LONGTEXT", "LOOP",
        "LOW_PRIORITY", "MASTER_SSL_VERIFY_SERVER_CERT", "MATCH", "MAXVALUE", "MEDIUMBLOB",
        "MEDIUMINT", "MEDIUMTEXT", "MIDDLEINT", "MINUTE_MICROSECOND", "MINUTE_SECOND", "MOD",
        "MODIFIES", "NATURAL", "NOT", "NO_WRITE_TO_BINLOG", "NULL", "NUMERIC", "ON", "OPTIMIZE",
        "OPTION", "OPTIONALLY", "OR", "ORDER", "OUT", "OUTER", "OUTFILE", "PRECISION", "PRIMARY",
        "PROCEDURE", "PURGE", "RANGE", "READ", "READS", "READ_WRITE", "REAL", "REFERENCES",
        "REGEXP", "RELEASE", "RENAME", "REPEAT", "REPLACE", "REQUIRE", "RESIGNAL", "RESTRICT",
        "RETURN", "REVOKE", "RIGHT", "RLIKE", "SCHEMA", "SCHEMAS", "SECOND_MICROSECOND", "SELECT",
        "SENSITIVE", "SEPARATOR", "SET", "SHOW", "SIGNAL", "SMALLINT", "SPATIAL", "SPECIFIC",
        "SQL", "SQLEXCEPTION", "SQLSTATE", "SQLWARNING", "SQL_BIG_RESULT", "SQL_CALC_FOUND_ROWS",
        "SQL_SMALL_RESULT", "SSL", "STARTING", "STRAIGHT_JOIN", "TABLE", "TERMINATED", "THEN",
        "TINYBLOB", "TINYINT", "TINYTEXT", "TO", "TRAILING", "TRIGGER", "TRUE", "UNDO", "UNION",
        "UNIQUE", "UNLOCK", "UNSIGNED", "UPDATE", "USAGE", "USE", "USING", "UTC_DATE", "UTC_TIME",
        "UTC_TIMESTAMP", "VALUES", "VARBINARY", "VARCHAR", "VARCHARACTER", "VARYING", "WHEN",
        "WHERE", "WHILE", "WITH", "WRITE", "XOR", "YEAR_MONTH", "ZEROFILL", "ORDER", "USER"
    };

    public string GetDefaultSchema() => string.Empty; // MySQL uses databases, not schemas

    public bool IsKeyword(string identifier) => identifier is not null && Keywords.Contains(identifier);

    public string EscapeTableName(string? schemaName, string tableName)
    {
        var escapedTable = IsKeyword(tableName) ? $"`{tableName}`" : tableName;

        if (string.IsNullOrWhiteSpace(schemaName))
            return escapedTable;

        var escapedSchema = (schemaName is not null && IsKeyword(schemaName)) ? $"`{schemaName}`" : schemaName;
        return $"{escapedSchema}.{escapedTable}";
    }

    public string EscapeColumnName(string columnName)
    {
        return IsKeyword(columnName) ? $"`{columnName}`" : columnName;
    }

    public string GetLastInsertIdSql(params string[] columnNames)
    {
        return "SELECT LAST_INSERT_ID();";
    }

    public string GetPagingSql(string baseSql, int offset, int fetchNext)
    {
        return $"{baseSql} LIMIT {offset}, {fetchNext}";
    }

    public string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // MySQL: Default LIKE is case-insensitive
        // We need to use a case-sensitive collation
        // utf8mb4_bin provides binary comparison (case-sensitive)
        // This works for both utf8 and utf8mb4 character sets
        return $"{columnName} COLLATE utf8mb4_bin LIKE {parameterName} ESCAPE '{escapeChar}'";

        // Alternative using BINARY keyword (also works but less explicit):
        // return $"BINARY {columnName} LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar)
    {
        // MySQL: Default LIKE is already case-insensitive
        // Just use standard LIKE without any collation
        // This uses the column's default collation (typically utf8mb4_general_ci)
        return $"{columnName} LIKE {parameterName} ESCAPE '{escapeChar}'";
    }

    public string GenerateCaseInsensitiveEquals(string columnName, string parameterName)
    {
        // MySQL: Default = is case-insensitive for most collations
        // Use utf8mb4_general_ci to be explicit
        return $"{columnName} COLLATE utf8mb4_general_ci = {parameterName}";
    }

    public string FormatContainsPattern(string value) => $"%{value}%";
    public string FormatStartsWithPattern(string value) => $"{value}%";
    public string FormatEndsWithPattern(string value) => $"%{value}";

    public string? GetDisableForeignKeyChecksSql() => "SET FOREIGN_KEY_CHECKS = 0";

    public string? GetEnableForeignKeyChecksSql() => "SET FOREIGN_KEY_CHECKS = 1";

    public bool SupportsForeignKeyToggle => true;

    public string GenerateCoalesce(params string[] expressions)
    {
        return $"COALESCE({string.Join(", ", expressions)})";
    }

    public string GenerateIsNull(string expression, string defaultExpression)
    {
        // MySQL uses IFNULL for the IsNull function
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
    public string GenerateSubstring(string expression, string start, string length) => $"SUBSTRING({expression}, {start}, {length})";

    // Date functions - MySQL uses YEAR(), MONTH(), DAY()
    public string GenerateYear(string expression) => $"YEAR({expression})";
    public string GenerateMonth(string expression) => $"MONTH({expression})";
    public string GenerateDay(string expression) => $"DAY({expression})";

    // Multi-row insert support
    public bool SupportsMultiRowInsert => true;
    public int MaxParametersPerStatement => 65535;

    // Upsert support - MySQL uses ON DUPLICATE KEY UPDATE
    public bool SupportsUpsert => true;

    public string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns)
    {
        // MySQL: INSERT INTO table (...) VALUES (...) ON DUPLICATE KEY UPDATE col = VALUES(col)
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

        sb.Append(") ON DUPLICATE KEY UPDATE ");

        for (int i = 0; i < updateColumns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(updateColumns[i]);
            sb.Append(" = VALUES(");
            sb.Append(updateColumns[i]);
            sb.Append(")");
        }

        return sb.ToString();
    }

    // Window functions - MySQL 8.0+ supports standard SQL:2003 window functions
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

    // Bulk copy support - MySQL has LOAD DATA INFILE
    // Implementation moved to Jaunty.Extensions.Reflection (optional package)
    public bool SupportsNativeBulkCopy => false;

    public IBulkCopyProvider? CreateBulkCopyProvider()
    {
        return null; // Requires Jaunty.Extensions.Reflection package
    }
}
