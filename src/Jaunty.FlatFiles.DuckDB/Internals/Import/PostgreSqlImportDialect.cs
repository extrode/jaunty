using System.Text;

using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Import dialect for PostgreSQL databases.
/// </summary>
internal sealed class PostgreSqlImportDialect : IImportDialect
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static PostgreSqlImportDialect Instance { get; } = new();

    /// <inheritdoc />
    public string MapClrTypeToSqlType(Type clrType) => clrType switch
    {
        _ when clrType == typeof(string) => "TEXT",
        _ when clrType == typeof(int) => "INTEGER",
        _ when clrType == typeof(long) => "BIGINT",
        _ when clrType == typeof(short) => "SMALLINT",
        _ when clrType == typeof(byte) => "SMALLINT",
        _ when clrType == typeof(float) => "REAL",
        _ when clrType == typeof(double) => "DOUBLE PRECISION",
        _ when clrType == typeof(decimal) => "NUMERIC",
        _ when clrType == typeof(bool) => "BOOLEAN",
        _ when clrType == typeof(DateTime) => "TIMESTAMP",
        _ when clrType == typeof(DateTimeOffset) => "TIMESTAMPTZ",
        _ when clrType == typeof(Guid) => "UUID",
        _ when clrType == typeof(byte[]) => "BYTEA",
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
        sb.Append($"INSERT INTO \"{tableName}\"");

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

        if (keyColumnName is not null)
        {
            if (conflictStrategy == ConflictStrategy.Skip)
            {
                sb.Append($" ON CONFLICT (\"{keyColumnName}\") DO NOTHING");
            }
            else if (conflictStrategy == ConflictStrategy.Upsert)
            {
                sb.Append($" ON CONFLICT (\"{keyColumnName}\") DO UPDATE SET ");
                var first = true;
                foreach (var colName in columnNames)
                {
                    if (colName == keyColumnName) continue;
                    if (!first) sb.Append(", ");
                    sb.Append($"\"{colName}\" = EXCLUDED.\"{colName}\"");
                    first = false;
                }
            }
        }

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