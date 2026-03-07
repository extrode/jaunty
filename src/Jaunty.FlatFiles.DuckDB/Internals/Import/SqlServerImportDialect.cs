using System.Text;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Import dialect for SQL Server databases.
/// </summary>
internal sealed class SqlServerImportDialect : IImportDialect
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static SqlServerImportDialect Instance { get; } = new();

    /// <inheritdoc />
    public string MapClrTypeToSqlType(Type clrType) => clrType switch
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
        _ => "NVARCHAR(MAX)"
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
        if (conflictStrategy != ConflictStrategy.Error && keyColumnName is not null)
        {
            return GenerateMergeSql(tableName, columnNames, parameterNames, conflictStrategy, keyColumnName);
        }

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
        sb.Append($"MERGE \"{tableName}\" AS target USING (SELECT ");

        for (int i = 0; i < parameterNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"{parameterNames[i]} AS \"{columnNames[i]}\"");
        }

        sb.Append($") AS source ON target.\"{keyColumnName}\" = source.\"{keyColumnName}\"");

        if (conflictStrategy == ConflictStrategy.Upsert)
        {
            sb.Append(" WHEN MATCHED THEN UPDATE SET ");
            var first = true;
            foreach (var colName in columnNames)
            {
                if (colName == keyColumnName) continue;
                if (!first) sb.Append(", ");
                sb.Append($"target.\"{colName}\" = source.\"{colName}\"");
                first = false;
            }
        }

        sb.Append(" WHEN NOT MATCHED THEN INSERT (");
        for (int i = 0; i < columnNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"\"{columnNames[i]}\"");
        }
        sb.Append(") VALUES (");
        for (int i = 0; i < columnNames.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"source.\"{columnNames[i]}\"");
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
        sb.Append($"IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}') ");
        sb.Append($"CREATE TABLE \"{tableName}\" (");

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
