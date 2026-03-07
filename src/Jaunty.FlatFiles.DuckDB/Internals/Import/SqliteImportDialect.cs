using System.Text;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Import dialect for SQLite databases.
/// </summary>
public sealed class SqliteImportDialect : IImportDialect
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static SqliteImportDialect Instance { get; } = new();

    /// <inheritdoc />
    public string MapClrTypeToSqlType(Type clrType) => clrType switch
    {
        _ when clrType == typeof(string) => "TEXT",
        _ when clrType == typeof(int) => "INTEGER",
        _ when clrType == typeof(long) => "INTEGER",
        _ when clrType == typeof(short) => "INTEGER",
        _ when clrType == typeof(byte) => "INTEGER",
        _ when clrType == typeof(float) => "REAL",
        _ when clrType == typeof(double) => "REAL",
        _ when clrType == typeof(decimal) => "REAL",
        _ when clrType == typeof(bool) => "INTEGER",
        _ when clrType == typeof(DateTime) => "TEXT",
        _ when clrType == typeof(DateTimeOffset) => "TEXT",
        _ when clrType == typeof(Guid) => "TEXT",
        _ when clrType == typeof(byte[]) => "BLOB",
        _ => "TEXT"
    };

    /// <inheritdoc />
    public string GenerateInsertSql(
        string tableName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> parameterNames,
        ConflictStrategy conflictStrategy,
        string? keyColumnName)
    {
        var sb = new StringBuilder();

        sb.Append(conflictStrategy switch
        {
            ConflictStrategy.Skip => $"INSERT OR IGNORE INTO \"{tableName}\"",
            ConflictStrategy.Upsert => $"INSERT OR REPLACE INTO \"{tableName}\"",
            _ => $"INSERT INTO \"{tableName}\""
        });

        sb.Append(" (");
        for (int i = 0; i < columnNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"\"{columnNames[i]}\"");
        }
        sb.Append(") VALUES (");
        for (int i = 0; i < parameterNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(parameterNames[i]);
        }
        sb.Append(')');

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateCreateTableSql(
        string tableName,
        IReadOnlyList<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns)
    {
        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE IF NOT EXISTS \"{tableName}\" (");

        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var (name, clrType, isPrimaryKey, isNullable) = columns[i];
            sb.Append($"\"{name}\" {MapClrTypeToSqlType(clrType)}");
            if (isPrimaryKey) sb.Append(" PRIMARY KEY");
            if (!isNullable && !isPrimaryKey) sb.Append(" NOT NULL");
        }

        sb.Append(')');
        return sb.ToString();
    }
}
