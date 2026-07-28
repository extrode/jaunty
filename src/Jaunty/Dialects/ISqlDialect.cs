using Jaunty.Configuration;

namespace Jaunty.Dialects;

/// <summary>
/// Database-specific SQL dialect for identifier escaping and SQL generation.
/// Only escapes identifiers that are SQL keywords to keep SQL readable.
/// </summary>
public interface ISqlDialect
{
    /// <summary>
    /// Gets the default schema for this database.
    /// </summary>
    string GetDefaultSchema();

    /// <summary>
    /// Gets the parameter prefix used in SQL queries (e.g., "@" for SQL Server/SQLite, "$" for DuckDB).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Honored by <c>Jaunty.Fluent</c> only.</b> Every fluent builder consults this property when
    /// composing placeholders. Jaunty core's own CRUD and by-id paths do not: <c>CrudSqlCache</c>,
    /// <c>MultiRowInsertCache</c>, <c>Upsert</c>, <c>DeleteCore</c>, <c>GetCore</c> and
    /// <c>WriteParameterHelper</c> all emit a literal <c>"@"</c>, and so do the two entity binders -
    /// <c>JauntyGenerator</c>'s generated <c>BindInsert</c>/<c>BindUpdate</c>/<c>BindDelete</c> and
    /// <c>Jaunty.Extensions.Reflection</c>'s equivalents.
    /// </para>
    /// <para>
    /// This is documented rather than fixed because the generated binders bake the prefix in at
    /// <em>compile</em> time, when no connection and therefore no dialect exists. Routing core CRUD
    /// through this property requires the generated binder contract to take a prefix at runtime,
    /// which is a breaking change to the generated API surface rather than an internal one - see
    /// AUD-R25 (B4-3).
    /// </para>
    /// <para>
    /// Consequence for a dialect that returns anything other than <c>"@"</c>: core CRUD produces SQL
    /// its own provider will not accept. <c>DuckDbDialect.ParameterPrefix</c> returns <c>"$"</c> and
    /// <c>DuckDb</c>'s constructor registers that dialect for <c>DuckDBConnection</c> by default, so
    /// <c>duckDbConnection.Insert(entity)</c> throws
    /// <c>DuckDBException: Binder Error: Referenced column "..." not found in FROM clause!</c> -
    /// DuckDB reads <c>@Name</c> as a column reference, not a placeholder.
    /// </para>
    /// <para>
    /// <b>Jaunty.Fluent is not a workaround for this.</b> It honours the prefix, so its SQL parses,
    /// but it then fails at bind time with
    /// <c>Invalid Input Error: Values were not provided for the following prepared statement
    /// parameters</c> - the names it gives its parameters are not the ones DuckDB matches against
    /// the <c>$</c> placeholders. Writes and by-id lookups against a non-<c>"@"</c> dialect
    /// therefore do not work through either path today, for two different reasons, and fixing core
    /// alone would not make them work. Reads are unaffected, since they bind no parameters. All
    /// three behaviours are pinned by <c>ParameterPrefixLimitationTests</c>.
    /// </para>
    /// </remarks>
    string ParameterPrefix { get; }

    /// <summary>
    /// Escapes a table name with schema support. Only escapes if necessary.
    /// </summary>
    string EscapeTableName(string? schemaName, string tableName);

    /// <summary>
    /// Escapes a column name. Only escapes if necessary.
    /// </summary>
    string EscapeColumnName(string columnName);

    /// <summary>
    /// Escapes a string value for safe inline use as a SQL string literal (i.e. between single
    /// quotes), for dialects/clauses that cannot use a bound parameter (e.g. HAVING literals).
    /// Dialects where backslash is an in-string escape character (e.g. MySQL/MariaDB without
    /// NO_BACKSLASH_ESCAPES) must also double backslashes, not just single quotes.
    /// </summary>
    string EscapeStringLiteral(string value);

    /// <summary>
    /// Returns SQL to retrieve the last inserted identity value.
    /// </summary>
    /// <param name="columnNames">The primary key column names (escaped if necessary).</param>
    string GetLastInsertIdSql(params string[] columnNames);

    /// <summary>
    /// Generates paging SQL (OFFSET/FETCH or LIMIT).
    /// </summary>
    string GetPagingSql(string baseSql, int offset, int fetchNext);

    /// <summary>
    /// Checks if an identifier is a SQL keyword that needs escaping.
    /// </summary>
    bool IsKeyword(string identifier);

    /// <summary>
    /// Generates a case-sensitive LIKE pattern.
    /// C# string methods (Contains, StartsWith, EndsWith) are case-sensitive by default,
    /// so we need to generate database-specific SQL to match that behavior.
    /// </summary>
    /// <param name="columnName">The column name (already escaped if needed)</param>
    /// <param name="parameterName">The parameter placeholder (e.g., @p0)</param>
    /// <param name="escapeChar">The escape character for LIKE wildcards (typically '[')</param>
    /// <returns>SQL LIKE expression with case-sensitive comparison</returns>
    string GenerateCaseSensitiveLike(string columnName, string parameterName, string escapeChar);

    /// <summary>
    /// Generates a case-insensitive LIKE pattern.
    /// Used when StringComparison.OrdinalIgnoreCase or CurrentCultureIgnoreCase is specified.
    /// </summary>
    /// <param name="columnName">The column name (already escaped if needed)</param>
    /// <param name="parameterName">The parameter placeholder (e.g., @p0)</param>
    /// <param name="escapeChar">The escape character for LIKE wildcards (typically '[')</param>
    /// <returns>SQL LIKE expression with case-insensitive comparison</returns>
    string GenerateCaseInsensitiveLike(string columnName, string parameterName, string escapeChar);

    /// <summary>
    /// Generates a case-insensitive equals comparison.
    /// Used when string.Equals with StringComparison.OrdinalIgnoreCase is specified.
    /// </summary>
    /// <param name="columnName">The column name (already escaped if needed)</param>
    /// <param name="parameterName">The parameter placeholder (e.g., @p0)</param>
    /// <returns>SQL equals expression with case-insensitive comparison</returns>
    string GenerateCaseInsensitiveEquals(string columnName, string parameterName);

    /// <summary>
    /// Formats a value for case-sensitive Contains pattern matching.
    /// </summary>
    /// <param name="value">The search value (already escaped for pattern matching)</param>
    /// <returns>Pattern value with appropriate wildcards for the dialect</returns>
    string FormatContainsPattern(string value);

    /// <summary>
    /// Formats a value for case-sensitive StartsWith pattern matching.
    /// </summary>
    /// <param name="value">The search value (already escaped for pattern matching)</param>
    /// <returns>Pattern value with appropriate wildcards for the dialect</returns>
    string FormatStartsWithPattern(string value);

    /// <summary>
    /// Formats a value for case-sensitive EndsWith pattern matching.
    /// </summary>
    /// <param name="value">The search value (already escaped for pattern matching)</param>
    /// <returns>Pattern value with appropriate wildcards for the dialect</returns>
    string FormatEndsWithPattern(string value);

    /// <summary>
    /// Formats a boolean literal for use directly in generated SQL (e.g. bare boolean member
    /// predicates such as <c>.Where(p => p.IsActive)</c>).
    /// </summary>
    /// <param name="value">The boolean value to format.</param>
    /// <returns>The dialect-appropriate SQL literal for the given boolean value.</returns>
    string FormatBooleanLiteral(bool value);

    /// <summary>
    /// Returns SQL to disable foreign key constraint checks for the current session/connection.
    /// Used by bulk operations that need to ignore referential integrity.
    /// </summary>
    /// <returns>SQL statement(s) to disable FK checks, or null if not supported.</returns>
    string? GetDisableForeignKeyChecksSql();

    /// <summary>
    /// Returns SQL to re-enable foreign key constraint checks for the current session/connection.
    /// Must be called after GetDisableForeignKeyChecksSql() to restore normal behavior.
    /// </summary>
    /// <returns>SQL statement(s) to enable FK checks, or null if not supported.</returns>
    string? GetEnableForeignKeyChecksSql();

    /// <summary>
    /// Indicates whether this dialect supports session-level foreign key constraint toggling.
    /// </summary>
    bool SupportsForeignKeyToggle { get; }

    /// <summary>
    /// Indicates whether <see cref="GetDisableForeignKeyChecksSql"/>/<see cref="GetEnableForeignKeyChecksSql"/>
    /// only take effect outside an active transaction (autocommit mode) and are a silent no-op
    /// while a transaction is pending. True for SQLite's <c>PRAGMA foreign_keys</c>; false for
    /// dialects whose FK-toggle statement is an ordinary session-level command that works fine
    /// inside a transaction.
    /// </summary>
    bool RequiresAutocommitForForeignKeyToggle { get; }

    /// <summary>
    /// Generates SQL for COALESCE function.
    /// Returns the first non-null value among the arguments.
    /// </summary>
    /// <param name="expressions">The SQL expressions to coalesce (already formatted with parameters).</param>
    /// <returns>COALESCE SQL expression.</returns>
    string GenerateCoalesce(params string[] expressions);

    /// <summary>
    /// Generates SQL for IsNull/IfNull function.
    /// Returns the second value if the first is null.
    /// </summary>
    /// <param name="expression">The SQL expression to check for null.</param>
    /// <param name="defaultExpression">The default value expression if null.</param>
    /// <returns>Dialect-specific IsNull SQL expression.</returns>
    string GenerateIsNull(string expression, string defaultExpression);

    /// <summary>
    /// Generates SQL for NULLIF function.
    /// Returns null if the two values are equal, otherwise returns the first value.
    /// </summary>
    /// <param name="expression">The SQL expression to check.</param>
    /// <param name="compareExpression">The comparison value expression.</param>
    /// <returns>NULLIF SQL expression.</returns>
    string GenerateNullIf(string expression, string compareExpression);

    // ==========================================
    // String Functions
    // ==========================================

    /// <summary>
    /// Generates SQL for string length function.
    /// SQL Server uses LEN, others use LENGTH.
    /// </summary>
    /// <param name="expression">The SQL expression for the string.</param>
    /// <returns>Dialect-specific length SQL expression.</returns>
    string GenerateLength(string expression);

    /// <summary>
    /// Generates SQL for uppercase conversion.
    /// All dialects use UPPER.
    /// </summary>
    /// <param name="expression">The SQL expression for the string.</param>
    /// <returns>UPPER SQL expression.</returns>
    string GenerateUpper(string expression);

    /// <summary>
    /// Generates SQL for lowercase conversion.
    /// All dialects use LOWER.
    /// </summary>
    /// <param name="expression">The SQL expression for the string.</param>
    /// <returns>LOWER SQL expression.</returns>
    string GenerateLower(string expression);

    /// <summary>
    /// Generates SQL for trimming whitespace.
    /// All dialects, including SQL Server, use TRIM (SQL Server 2017+ syntax) - there is no
    /// LTRIM(RTRIM(...)) fallback for older SQL Server versions.
    /// </summary>
    /// <param name="expression">The SQL expression for the string.</param>
    /// <returns>Dialect-specific trim SQL expression.</returns>
    string GenerateTrim(string expression);

    /// <summary>
    /// Generates SQL for substring extraction.
    /// SQL Server/PostgreSQL/MySQL use SUBSTRING, SQLite uses SUBSTR.
    /// </summary>
    /// <param name="expression">The SQL expression for the string.</param>
    /// <param name="start">The start position SQL expression (1-based).</param>
    /// <param name="length">The length SQL expression.</param>
    /// <returns>Dialect-specific substring SQL expression.</returns>
    string GenerateSubstring(string expression, string start, string length);

    // ==========================================
    // Date Functions
    // ==========================================

    /// <summary>
    /// Generates SQL to extract the year from a date/datetime.
    /// SQL Server/MySQL use YEAR, SQLite uses strftime, PostgreSQL uses EXTRACT.
    /// </summary>
    /// <param name="expression">The SQL expression for the date/datetime.</param>
    /// <returns>Dialect-specific year extraction SQL expression.</returns>
    string GenerateYear(string expression);

    /// <summary>
    /// Generates SQL to extract the month from a date/datetime.
    /// SQL Server/MySQL use MONTH, SQLite uses strftime, PostgreSQL uses EXTRACT.
    /// </summary>
    /// <param name="expression">The SQL expression for the date/datetime.</param>
    /// <returns>Dialect-specific month extraction SQL expression.</returns>
    string GenerateMonth(string expression);

    /// <summary>
    /// Generates SQL to extract the day of month from a date/datetime.
    /// SQL Server/MySQL use DAY, SQLite uses strftime, PostgreSQL uses EXTRACT.
    /// </summary>
    /// <param name="expression">The SQL expression for the date/datetime.</param>
    /// <returns>Dialect-specific day extraction SQL expression.</returns>
    string GenerateDay(string expression);

    // ==========================================
    // Upsert Support
    // ==========================================

    /// <summary>
    /// Indicates whether this dialect supports upsert operations.
    /// </summary>
    bool SupportsUpsert { get; }

    /// <summary>
    /// Generates SQL for an upsert (INSERT or UPDATE if exists) operation.
    /// </summary>
    /// <param name="tableName">The escaped table name.</param>
    /// <param name="insertColumns">Column names for INSERT.</param>
    /// <param name="insertParams">Parameter names for INSERT values.</param>
    /// <param name="updateColumns">Column names for UPDATE SET clause.</param>
    /// <param name="updateParams">Parameter names for UPDATE values.</param>
    /// <param name="keyColumns">Primary key column names for conflict detection.</param>
    /// <param name="keyParams">Parameter names for the primary key values (needed even for
    /// identity keys excluded from <paramref name="insertColumns"/>, since MERGE-based
    /// dialects still need to match on them).</param>
    /// <returns>Dialect-specific upsert SQL statement.</returns>
    string GenerateUpsertSql(
        string tableName,
        string[] insertColumns,
        string[] insertParams,
        string[] updateColumns,
        string[] updateParams,
        string[] keyColumns,
        string[] keyParams);

    // ==========================================
    // Multi-Row Insert Support
    // ==========================================

    /// <summary>
    /// Indicates whether this dialect supports multi-row INSERT VALUES syntax.
    /// </summary>
    bool SupportsMultiRowInsert { get; }

    /// <summary>
    /// Gets the maximum number of parameters allowed per SQL statement.
    /// Used to compute optimal batch sizes for multi-row INSERT operations.
    /// </summary>
    int MaxParametersPerStatement { get; }

    // ==========================================
    // Window Functions
    // ==========================================

    /// <summary>
    /// Generates SQL for ROW_NUMBER() window function.
    /// </summary>
    /// <returns>ROW_NUMBER() SQL fragment.</returns>
    string GenerateRowNumber();

    /// <summary>
    /// Generates SQL for RANK() window function.
    /// </summary>
    /// <returns>RANK() SQL fragment.</returns>
    string GenerateRank();

    /// <summary>
    /// Generates SQL for DENSE_RANK() window function.
    /// </summary>
    /// <returns>DENSE_RANK() SQL fragment.</returns>
    string GenerateDenseRank();

    /// <summary>
    /// Generates SQL for NTILE(n) window function.
    /// </summary>
    /// <param name="buckets">The number of buckets to distribute rows into.</param>
    /// <returns>NTILE(n) SQL fragment.</returns>
    string GenerateNTile(int buckets);

    /// <summary>
    /// Generates the OVER clause for a window function.
    /// </summary>
    /// <param name="partitionBy">Columns for PARTITION BY (null if none).</param>
    /// <param name="orderBy">Columns for ORDER BY with direction (null if none).</param>
    /// <returns>OVER(...) SQL fragment.</returns>
    string GenerateOverClause(string[]? partitionBy, (string column, bool descending)[]? orderBy);

    /// <summary>
    /// Generates SQL for a windowed aggregate function (SUM, AVG, COUNT, MIN, MAX with OVER).
    /// </summary>
    /// <param name="function">The aggregate function name (SUM, AVG, COUNT, MIN, MAX).</param>
    /// <param name="expression">The expression to aggregate (null for COUNT(*)).</param>
    /// <returns>Aggregate function SQL fragment.</returns>
    string GenerateWindowAggregate(string function, string? expression);

    // ==========================================
    // Bulk Copy Support
    // ==========================================

    /// <summary>
    /// Indicates whether this dialect supports native bulk copy operations.
    /// Native bulk copy uses database-specific APIs (e.g., SqlBulkCopy, NpgsqlBinaryImporter)
    /// for high-performance data loading.
    /// </summary>
    bool SupportsNativeBulkCopy { get; }

    /// <summary>
    /// Creates a bulk copy provider for this dialect.
    /// </summary>
    /// <returns>An <see cref="IBulkCopyProvider"/> instance, or null if not supported.</returns>
    IBulkCopyProvider? CreateBulkCopyProvider();
}