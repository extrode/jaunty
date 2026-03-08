using System.Reflection;

using Jaunty.Attributes;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Generates CREATE TABLE DDL for target databases based on entity metadata,
/// delegating database-specific type mapping to an <see cref="IImportDialect"/>.
/// </summary>
internal static class TargetDdlGenerator
{
    /// <summary>
    /// Generates a CREATE TABLE IF NOT EXISTS statement for the target database
    /// using the provided import dialect.
    /// </summary>
    public static string GenerateCreateTableSql(Type entityType, string tableName, IImportDialect dialect)
    {
        var columns = GetColumnDefinitions(entityType);
        return dialect.GenerateCreateTableSql(tableName, columns);
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

    internal static List<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> GetColumnDefinitions(Type entityType)
    {
        var result = new List<(string, Type, bool, bool)>();

        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;

            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var columnName = colAttr?.Name ?? prop.Name;
            var isPrimaryKey = prop.GetCustomAttribute<KeyAttribute>() is not null;

            var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            var isNullable = Nullable.GetUnderlyingType(prop.PropertyType) is not null
                || (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string));

            if (prop.PropertyType == typeof(string))
            {
                isNullable = IsNullableReferenceType(prop);
            }

            result.Add((columnName, underlyingType, isPrimaryKey, isNullable));
        }

        return result;
    }

    private static bool IsNullableReferenceType(PropertyInfo prop)
    {
        var context = new NullabilityInfoContext();
        var nullabilityInfo = context.Create(prop);
        return nullabilityInfo.WriteState == NullabilityState.Nullable;
    }
}