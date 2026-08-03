using System.Globalization;
using System.Text;

using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Import dialect for SQLite databases.
/// </summary>
internal sealed class SqliteImportDialect : IImportDialect, IQuotedIdentifierDialect, IImportValueTransform
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
    public string MapClrTypeToSqlType(Type clrType) => MapNormalized(ImportTypeMapping.Normalize(clrType));

    private static string MapNormalized(Type clrType) => clrType switch
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
        _ when clrType == typeof(DateOnly) => "TEXT",
        _ when clrType == typeof(TimeOnly) => "TEXT",
        _ when clrType == typeof(TimeSpan) => "TEXT",
        _ when clrType == typeof(char) => "TEXT",
        _ when clrType == typeof(uint) => "INTEGER",
        // AUD-R34-029: SQLite's INTEGER storage class is a signed 64-bit two's-complement value, so
        // the top ~47% of ulong's range cannot be represented as declared - and the provider does
        // not complain, it reinterprets: ulong.MaxValue arrives as -1. SqlServerImportDialect and
        // PostgreSqlImportDialect widen to DECIMAL(20,0)/NUMERIC(20,0) for exactly this reason, but
        // SQLite has no unsigned and no 128-bit integer type, so the representation has to change
        // rather than widen. TEXT holds the decimal digits losslessly; TryTransformForBinding below
        // is the other half, because the declared type alone does not stop the provider's
        // reinterpretation.
        _ when clrType == typeof(ulong) => "TEXT",
        _ when clrType == typeof(sbyte) => "INTEGER",
        _ when clrType == typeof(ushort) => "INTEGER",
        _ => throw ImportTypeMapping.Unsupported(clrType, "SQLite")
    };

    /// <summary>
    /// AUD-R34-029: binds a <see cref="ulong"/> as its invariant decimal text, matching the
    /// <c>TEXT</c> column <see cref="MapClrTypeToSqlType"/> declares for it. Without this the
    /// provider binds the value as a signed 64-bit integer and the column silently receives a
    /// negative number for anything above <see cref="long.MaxValue"/>.
    /// </summary>
    bool IImportValueTransform.TryTransformForBinding(object value, out object transformed)
    {
        if (value is ulong unsigned)
        {
            transformed = unsigned.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        transformed = value;
        return false;
    }

    /// <inheritdoc />
    public string GenerateInsertSql(
        string tableName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> parameterNames,
        ConflictStrategy conflictStrategy,
        string? keyColumnName)
    {
        // != Error, not == Upsert: Skip needs the key column just as much, both to name the
        // conflict target below and to match PostgreSqlImportDialect/SqlServerImportDialect, which
        // reject a keyless non-Error strategy rather than quietly doing something else.
        if (conflictStrategy != ConflictStrategy.Error && keyColumnName is null)
        {
            throw new NotSupportedException(
                $"Table '{tableName}' has no [Key] property to use for conflict resolution. " +
                $"The {conflictStrategy} conflict strategy requires a [Key]-attributed property; " +
                "use ConflictStrategy.Error (the default) instead, or add a [Key] attribute to the entity.");
        }

        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {QuoteIdentifier(tableName)}");

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

        if (conflictStrategy == ConflictStrategy.Skip)
        {
            // Scoped to the key column, not "INSERT OR IGNORE". OR IGNORE suppresses *every*
            // constraint violation on the row - NOT NULL, CHECK, foreign key, and any other UNIQUE
            // index - so a malformed source row was silently dropped and counted as "skipped a
            // duplicate". ON CONFLICT (key) DO NOTHING skips only the duplicate-key case and still
            // surfaces everything else, which is what PostgreSqlImportDialect's DO NOTHING and
            // SqlServerImportDialect's key-matched MERGE already do. Supported since SQLite 3.24 -
            // the same release that added the DO UPDATE form the Upsert branch below relies on.
            sb.Append($" ON CONFLICT ({QuoteIdentifier(keyColumnName!)}) DO NOTHING");
        }
        else if (conflictStrategy == ConflictStrategy.Upsert)
        {
            // SQLite's "INSERT ... ON CONFLICT DO UPDATE" (added in 3.24) performs a true
            // UPDATE, unlike "INSERT OR REPLACE" which is a DELETE+INSERT under the hood -
            // it fires UPDATE triggers instead of DELETE+INSERT triggers, doesn't churn the
            // rowid/AUTOINCREMENT counter, and doesn't cascade-delete FK-dependent child rows.
            // Key-only entity: there is nothing to update, and "DO UPDATE SET" with no
            // assignments is a syntax error, so degrade to DO NOTHING (matching
            // DuckDbDialect.GenerateUpsertSql's guard for the same case).
            var assignments = new StringBuilder();
            foreach (var colName in columnNames)
            {
                if (colName == keyColumnName) continue;
                if (assignments.Length > 0) assignments.Append(", ");
                assignments.Append($"{QuoteIdentifier(colName)} = excluded.{QuoteIdentifier(colName)}");
            }

            sb.Append(assignments.Length > 0
                ? $" ON CONFLICT ({QuoteIdentifier(keyColumnName!)}) DO UPDATE SET {assignments}"
                : $" ON CONFLICT ({QuoteIdentifier(keyColumnName!)}) DO NOTHING");
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
            sb.Append($"{QuoteIdentifier(name)} {ImportTypeMapping.MapForColumn(MapClrTypeToSqlType, name, clrType)}");
            if (isPrimaryKey) sb.Append(" PRIMARY KEY");

            // AUD-R35-029: NOT NULL is emitted for a non-nullable primary key too, unlike the
            // PostgreSQL and SQL Server dialects. There PRIMARY KEY implies NOT NULL; on SQLite it
            // does not. Outside an INTEGER PRIMARY KEY rowid alias - and inside a WITHOUT ROWID
            // table even then - SQLite accepts NULLs in a PRIMARY KEY column, a documented
            // long-standing bug it keeps for backwards compatibility. So a non-nullable
            // string/Guid/ulong key got "TEXT PRIMARY KEY" and a source row with an empty key
            // column imported as NULL. NOT NULL on an INTEGER PRIMARY KEY does not stop it being a
            // rowid alias, so the rowid case is unaffected.
            if (!isNullable) sb.Append(" NOT NULL");
        }

        sb.Append(')');
        return sb.ToString();
    }
}