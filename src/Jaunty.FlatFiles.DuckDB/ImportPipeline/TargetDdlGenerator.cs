using System.Data.Common;
using System.Reflection;
using System.Text;
using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.ImportPipeline;

/// <summary>
/// Generates CREATE TABLE DDL for target databases based on entity metadata.
/// Supports SQLite, PostgreSQL, and SQL Server type mappings.
/// </summary>
internal static class TargetDdlGenerator
{
    /// <summary>
    /// Generates a CREATE TABLE IF NOT EXISTS statement for the target database.
    /// </summary>
    public static string GenerateCreateTableSql(Type entityType, string tableName, DbConnection targetConnection)
    {
        var dbType = DetectDatabaseType(targetConnection);
        var mappings = GetColumnDefinitions(entityType, dbType);

        var sb = new StringBuilder();
        sb.Append($"CREATE TABLE IF NOT EXISTS \"{tableName}\" (");

        for (int i = 0; i < mappings.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var (columnName, sqlType, isPrimaryKey, isNullable) = mappings[i];
            sb.Append($"\"{columnName}\" {sqlType}");
            if (isPrimaryKey) sb.Append(" PRIMARY KEY");
            if (!isNullable && !isPrimaryKey) sb.Append(" NOT NULL");
        }

        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>
    /// Gets the key column name(s) for the entity type.
    /// </summary>
    public static string? GetKeyColumnName(Type entityType)
    {
        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetCustomAttribute<KeyAttribute>() is not null)
            {
                var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
                return colAttr?.Name ?? prop.Name;
            }
        }
        return null;
    }

    private static List<(string ColumnName, string SqlType, bool IsPrimaryKey, bool IsNullable)> GetColumnDefinitions(
        Type entityType, DatabaseType dbType)
    {
        var result = new List<(string, string, bool, bool)>();

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;

            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var columnName = colAttr?.Name ?? prop.Name;
            var isPrimaryKey = prop.GetCustomAttribute<KeyAttribute>() is not null;

            var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var isNullable = Nullable.GetUnderlyingType(prop.PropertyType) is not null
                || (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string));

            // String is nullable if property allows null (check NullableAttribute in practice,
            // but for simplicity treat string as NOT NULL by default unless it's string?)
            if (prop.PropertyType == typeof(string))
            {
                // Check for NullabilityInfoContext or just default to NOT NULL
                isNullable = IsNullableReferenceType(prop);
            }

            var sqlType = MapToSqlType(underlyingType, dbType);
            result.Add((columnName, sqlType, isPrimaryKey, isNullable));
        }

        return result;
    }

    private static bool IsNullableReferenceType(PropertyInfo prop)
    {
        // Use NullabilityInfoContext to check if a reference type is nullable
        var context = new NullabilityInfoContext();
        var nullabilityInfo = context.Create(prop);
        return nullabilityInfo.WriteState == NullabilityState.Nullable;
    }

    private static string MapToSqlType(Type clrType, DatabaseType dbType)
    {
        return dbType switch
        {
            DatabaseType.Sqlite => MapToSqliteType(clrType),
            DatabaseType.PostgreSql => MapToPostgresType(clrType),
            DatabaseType.SqlServer => MapToSqlServerType(clrType),
            _ => MapToSqliteType(clrType) // Default to SQLite-compatible types
        };
    }

    private static string MapToSqliteType(Type clrType) => clrType switch
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

    private static string MapToPostgresType(Type clrType) => clrType switch
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

    private static string MapToSqlServerType(Type clrType) => clrType switch
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

    private enum DatabaseType
    {
        Sqlite,
        PostgreSql,
        SqlServer,
        Unknown
    }

    private static DatabaseType DetectDatabaseType(DbConnection connection)
    {
        var typeName = connection.GetType().FullName ?? "";

        if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return DatabaseType.Sqlite;
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            return DatabaseType.PostgreSql;
        if (typeName.Contains("SqlConnection", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Microsoft.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
            return DatabaseType.SqlServer;

        return DatabaseType.Unknown;
    }
}
