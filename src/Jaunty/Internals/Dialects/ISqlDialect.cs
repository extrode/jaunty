namespace Jaunty.Internals.Dialects;

/// <summary>
/// Database-specific SQL dialect for identifier escaping and SQL generation.
/// Only escapes identifiers that are SQL keywords to keep SQL readable.
/// </summary>
internal interface ISqlDialect
{
    /// <summary>
    /// Gets the default schema for this database.
    /// </summary>
    string GetDefaultSchema();

    /// <summary>
    /// Escapes a table name with schema support. Only escapes if necessary.
    /// </summary>
    string EscapeTableName(string? schemaName, string tableName);

    /// <summary>
    /// Escapes a column name. Only escapes if necessary.
    /// </summary>
    string EscapeColumnName(string columnName);

    /// <summary>
    /// Returns SQL to retrieve the last inserted identity value.
    /// </summary>
    string GetLastInsertIdSql();

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
}
