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
}
