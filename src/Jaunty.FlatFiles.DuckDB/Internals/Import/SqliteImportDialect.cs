using System.Text;

using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Import dialect for SQLite databases.
/// </summary>
internal sealed class SqliteImportDialect : IImportDialect, IQuotedIdentifierDialect
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static SqliteImportDialect Instance { get; } = new();

    /// <summary>
    /// Quotes an identifier for SQLite, doubling any embedded double quotes so the
    /// identifier cannot break out of the quoted context (e.g. names derived from
    /// filenames or [Table]/[Column] attributes).
    /// </summary>
    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    /// <inheritdoc />
    string IQuotedIdentifierDialect.QuoteIdentifier(string identifier) => QuoteIdentifier(identifier);

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
        if (conflictStrategy == ConflictStrategy.Upsert && keyColumnName is null)
        {
            throw new NotSupportedException(
                $"Table '{tableName}' has no [Key] property to use for conflict resolution. " +
                $"The {conflictStrategy} conflict strategy requires a [Key]-attributed property; " +
                "use ConflictStrategy.Error (the default) instead, or add a [Key] attribute to the entity.");
        }

        var sb = new StringBuilder();

        sb.Append(conflictStrategy switch
        {
            ConflictStrategy.Skip => $"INSERT OR IGNORE INTO {QuoteIdentifier(tableName)}",
            _ => $"INSERT INTO {QuoteIdentifier(tableName)}"
        });

        sb.Append(" (");
        for (int i = 0; i < columnNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(QuoteIdentifier(columnNames[i]));
        }
        sb.Append(") VALUES (");
        for (int i = 0; i < parameterNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(parameterNames[i]);
        }
        sb.Append(')');

        if (conflictStrategy == ConflictStrategy.Upsert)
        {
            // SQLite's "INSERT ... ON CONFLICT DO UPDATE" (added in 3.24) performs a true
            // UPDATE, unlike "INSERT OR REPLACE" which is a DELETE+INSERT under the hood -
            // it fires UPDATE triggers instead of DELETE+INSERT triggers, doesn't churn the
            // rowid/AUTOINCREMENT counter, and doesn't cascade-delete FK-dependent child rows.
            sb.Append($" ON CONFLICT ({QuoteIdentifier(keyColumnName!)}) DO UPDATE SET ");
            var first = true;
            foreach (var colName in columnNames)
            {
                if (colName == keyColumnName) continue;
                if (!first) sb.Append(", ");
                sb.Append($"{QuoteIdentifier(colName)} = excluded.{QuoteIdentifier(colName)}");
                first = false;
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
        sb.Append($"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(tableName)} (");

        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            (string? name, Type? clrType, bool isPrimaryKey, bool isNullable) = columns[i];
            sb.Append($"{QuoteIdentifier(name)} {MapClrTypeToSqlType(clrType)}");
            if (isPrimaryKey) sb.Append(" PRIMARY KEY");
            if (!isNullable && !isPrimaryKey) sb.Append(" NOT NULL");
        }

        sb.Append(')');
        return sb.ToString();
    }
}