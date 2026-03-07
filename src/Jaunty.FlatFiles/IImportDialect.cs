using System.Data.Common;
using System.Text;

namespace Jaunty.FlatFiles;

/// <summary>
/// Provides database-specific SQL generation for the import pipeline.
/// Implement this interface to support importing flat file data into database engines
/// that are not natively supported (SQLite, PostgreSQL, SQL Server).
/// </summary>
public interface IImportDialect
{
    /// <summary>
    /// Maps a CLR type to the database-specific SQL type name.
    /// </summary>
    /// <param name="clrType">The CLR type (e.g. <see cref="int"/>, <see cref="string"/>, <see cref="decimal"/>).</param>
    /// <returns>The SQL type name (e.g. "INTEGER", "TEXT", "NUMERIC").</returns>
    string MapClrTypeToSqlType(Type clrType);

    /// <summary>
    /// Generates the INSERT SQL statement, including any conflict-handling clauses.
    /// </summary>
    /// <param name="tableName">The target table name.</param>
    /// <param name="columnNames">The column names to insert into.</param>
    /// <param name="parameterNames">The parameter placeholder names (e.g. "@p0", "@p1").</param>
    /// <param name="conflictStrategy">The conflict resolution strategy.</param>
    /// <param name="keyColumnName">The primary key column name, or null if none.</param>
    /// <returns>The complete INSERT SQL statement.</returns>
    string GenerateInsertSql(
        string tableName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> parameterNames,
        ConflictStrategy conflictStrategy,
        string? keyColumnName);

    /// <summary>
    /// Generates the CREATE TABLE IF NOT EXISTS SQL statement.
    /// </summary>
    /// <param name="tableName">The target table name.</param>
    /// <param name="columns">Column definitions: (name, clrType, isPrimaryKey, isNullable).</param>
    /// <returns>The CREATE TABLE SQL statement.</returns>
    string GenerateCreateTableSql(
        string tableName,
        IReadOnlyList<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns);
}
