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
        List<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> columns = GetColumnDefinitions(entityType);
        return dialect.GenerateCreateTableSql(tableName, columns);
    }

    /// <summary>
    /// Gets the key column name for the entity type.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown when the entity type has more than one <c>[Key]</c> property. Composite keys are not
    /// currently supported for import conflict resolution (ON CONFLICT / MERGE) or PRIMARY KEY DDL
    /// generation; silently using only the first key column would produce incorrect upsert matching.
    /// </exception>
    public static string? GetKeyColumnName(Type entityType)
    {
        PropertyInfo? keyProperty = null;
        string? keyColumnName = null;

        foreach (PropertyInfo prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!MappedPropertyFilter.IsMapped(prop)) continue;
            if (prop.GetCustomAttribute<KeyAttribute>() is null) continue;

            if (keyProperty is not null)
            {
                throw new NotSupportedException(
                    $"Entity type '{entityType.Name}' has more than one [Key] property " +
                    $"('{keyProperty.Name}' and '{prop.Name}'). Composite keys are not currently " +
                    "supported for import conflict resolution or PRIMARY KEY DDL generation.");
            }

            keyProperty = prop;
            ColumnAttribute? colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            keyColumnName = colAttr?.Name ?? prop.Name;
        }

        return keyColumnName;
    }

    internal static List<(string Name, Type ClrType, bool IsPrimaryKey, bool IsNullable)> GetColumnDefinitions(Type entityType)
    {
        var result = new List<(string, Type, bool, bool)>();
        // AUD-R26: this loop used to emit every mapped property, so two properties on one column
        // produced CREATE TABLE ("code", "CODE") - rejected by SQLite and SQL Server, and silently
        // half-populated on PostgreSQL where the quoted identifiers are distinct. ColumnMappingCache
        // meanwhile collapsed them to one. See DuplicateColumnGuard.
        Dictionary<string, string> claimed = DuplicateColumnGuard.NewClaimSet(4);

        foreach (PropertyInfo prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!MappedPropertyFilter.IsMapped(prop)) continue;

            ColumnAttribute? colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var columnName = colAttr?.Name ?? prop.Name;
            DuplicateColumnGuard.Claim(entityType, columnName, prop, claimed);
            var isPrimaryKey = prop.GetCustomAttribute<KeyAttribute>() is not null;

            Type underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
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

    // NullabilityInfoContext caches per-module/per-type nullability metadata internally, so
    // allocating one per property is wasteful. It is not documented as thread-safe, so each
    // thread gets its own cached instance instead of sharing one across threads.
    [ThreadStatic]
    private static NullabilityInfoContext? t_nullabilityContext;

    private static bool IsNullableReferenceType(PropertyInfo prop)
    {
        NullabilityInfoContext context = t_nullabilityContext ??= new NullabilityInfoContext();
        NullabilityInfo nullabilityInfo = context.Create(prop);
        return nullabilityInfo.WriteState == NullabilityState.Nullable;
    }
}