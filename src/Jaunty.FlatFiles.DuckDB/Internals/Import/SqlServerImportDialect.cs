using System.Text;

using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Import dialect for SQL Server databases.
/// </summary>
internal sealed class SqlServerImportDialect : IImportDialect, IQuotedIdentifierDialect
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static SqlServerImportDialect Instance { get; } = new();

    /// <summary>
    /// Quotes an identifier for SQL Server using [brackets], doubling any embedded closing
    /// bracket so the identifier cannot break out of the quoted context (e.g. names derived
    /// from filenames or [Table]/[Column] attributes). Brackets are SQL Server's native
    /// identifier delimiter and work regardless of the session's QUOTED_IDENTIFIER setting,
    /// unlike double quotes, which only work when QUOTED_IDENTIFIER is ON (see
    /// src/Jaunty/Dialects/SqlServerDialect.cs).
    /// </summary>
    private static string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]")}]";

    /// <inheritdoc />
    string IQuotedIdentifierDialect.QuoteIdentifier(string identifier) => QuoteIdentifier(identifier);

    /// <inheritdoc />
    public string MapClrTypeToSqlType(Type clrType) => MapNormalized(ImportTypeMapping.Normalize(clrType));

    private static string MapNormalized(Type clrType) => clrType switch
    {
        _ when clrType == typeof(string) => "NVARCHAR(MAX)",
        _ when clrType == typeof(int) => "INT",
        _ when clrType == typeof(long) => "BIGINT",
        _ when clrType == typeof(short) => "SMALLINT",
        _ when clrType == typeof(byte) => "TINYINT",
        _ when clrType == typeof(float) => "REAL",
        _ when clrType == typeof(double) => "FLOAT",
        _ when clrType == typeof(decimal) => "DECIMAL(18,4)",
        _ when clrType == typeof(bool) => "BIT",
        _ when clrType == typeof(DateTime) => "DATETIME2",
        _ when clrType == typeof(DateTimeOffset) => "DATETIMEOFFSET",
        _ when clrType == typeof(Guid) => "UNIQUEIDENTIFIER",
        _ when clrType == typeof(byte[]) => "VARBINARY(MAX)",
        _ when clrType == typeof(DateOnly) => "DATE",
        _ when clrType == typeof(TimeOnly) => "TIME",
        _ when clrType == typeof(TimeSpan) => "TIME",
        _ when clrType == typeof(char) => "NCHAR(1)",
        _ when clrType == typeof(uint) => "BIGINT",
        _ when clrType == typeof(ulong) => "DECIMAL(20,0)",
        _ when clrType == typeof(sbyte) => "SMALLINT",
        _ when clrType == typeof(ushort) => "INT",
        _ => throw ImportTypeMapping.Unsupported(clrType, "SQL Server")
    };

    /// <inheritdoc />
    public string GenerateInsertSql(
        string tableName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> parameterNames,
        ConflictStrategy conflictStrategy,
        string? keyColumnName)
    {
        // SQL Server uses MERGE for upsert/skip scenarios
        if (conflictStrategy != ConflictStrategy.Error)
        {
            if (keyColumnName is null)
            {
                throw new NotSupportedException(
                    $"Table '{tableName}' has no [Key] property to use for conflict resolution. " +
                    $"The {conflictStrategy} conflict strategy requires a [Key]-attributed property; " +
                    "use ConflictStrategy.Error (the default) instead, or add a [Key] attribute to the entity.");
            }

            return GenerateMergeSql(tableName, columnNames, parameterNames, conflictStrategy, keyColumnName);
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

        return sb.ToString();
    }

    private static string GenerateMergeSql(
        string tableName,
        IReadOnlyList<string> columnNames,
        IReadOnlyList<string> parameterNames,
        ConflictStrategy conflictStrategy,
        string keyColumnName)
    {
        var sb = new StringBuilder();
        sb.Append($"MERGE {QuoteIdentifier(tableName)} WITH (HOLDLOCK) AS target USING (SELECT ");

        for (int i = 0; i < parameterNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"{parameterNames[i]} AS {QuoteIdentifier(columnNames[i])}");
        }

        sb.Append($") AS source ON target.{QuoteIdentifier(keyColumnName)} = source.{QuoteIdentifier(keyColumnName)}");

        if (conflictStrategy == ConflictStrategy.Upsert)
        {
            sb.Append(" WHEN MATCHED THEN UPDATE SET ");
            var first = true;
            foreach (var colName in columnNames)
            {
                if (colName == keyColumnName) continue;
                if (!first) sb.Append(", ");
                sb.Append($"target.{QuoteIdentifier(colName)} = source.{QuoteIdentifier(colName)}");
                first = false;
            }
        }

        sb.Append(" WHEN NOT MATCHED THEN INSERT (");
        for (int i = 0; i < columnNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(QuoteIdentifier(columnNames[i]));
        }
        sb.Append(") VALUES (");
        for (int i = 0; i < columnNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"source.{QuoteIdentifier(columnNames[i])}");
        }
        sb.Append(");");

        return sb.ToString();
    }

    /// <inheritdoc />
    public string GenerateCreateTableSql(
        string tableName,
        IReadOnlyList<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns)
    {
        var sb = new StringBuilder();

        // AUD-R26-065: OBJECT_ID resolves the name the same way CREATE TABLE below will - in the
        // connection's default schema, or in an explicitly named one - instead of matching a bare
        // name across every schema in the database.
        //
        // The old guard was `IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '<table>')`, and
        // sys.tables holds one row per table per schema with no schema predicate applied. An
        // unrelated staging.orders or archive.orders therefore suppressed the creation of
        // dbo.orders, and the import's INSERT then ran against a table that was never made - or,
        // worse, against a same-named table in another schema that the connection's default schema
        // happened to resolve to. sys.tables exposes schema_id precisely so this can be qualified;
        // OBJECT_ID does the qualification for us and accepts a schema-qualified name unchanged.
        //
        // Still not atomic, and cannot be made so in one statement: two importers starting together
        // both pass the check and one gets "There is already an object named 'x'". PostgreSQL and
        // SQLite avoid this for free with CREATE TABLE IF NOT EXISTS, which SQL Server has no
        // equivalent of. Serialising it needs an application lock or a caught error 2714 around the
        // whole create, which is the caller's transaction to own rather than this generator's.
        sb.Append($"IF OBJECT_ID(N'{QuoteIdentifier(tableName).Replace("'", "''")}', N'U') IS NULL ");
        sb.Append($"CREATE TABLE {QuoteIdentifier(tableName)} (");

        for (int i = 0; i < columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            (string? name, Type? clrType, bool isPrimaryKey, bool isNullable) = columns[i];
            sb.Append($"{QuoteIdentifier(name)} {ImportTypeMapping.MapForColumn(MapClrTypeToSqlType, name, clrType)}");
            if (isPrimaryKey) sb.Append(" PRIMARY KEY");
            if (!isNullable && !isPrimaryKey) sb.Append(" NOT NULL");
        }

        sb.Append(')');
        return sb.ToString();
    }
}