using System.Collections.Concurrent;
using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Configuration;

namespace Jaunty.Internal;

internal static class NameResolver
{
    private static readonly ConcurrentDictionary<Type, string> TableNameCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, string> ColumnNameCache = new();

    /// <summary>
    /// Resolves table name for entity type.
    /// Priority: 1) TableAttribute 2) JauntyConfig.TableNameResolver 3) Type name
    /// </summary>
    public static string GetTableName(Type type)
    {
        return TableNameCache.GetOrAdd(type, ResolveTableName);
    }

    /// <summary>
    /// Resolves column name for property.
    /// Priority: 1) ColumnAttribute 2) JauntyConfig.ColumnNameResolver 3) Property name
    /// </summary>
    public static string GetColumnName(PropertyInfo property)
    {
        return ColumnNameCache.GetOrAdd(property, ResolveColumnName);
    }

    /// <summary>
    /// Checks if property should be ignored.
    /// </summary>
    public static bool IsIgnored(PropertyInfo property)
    {
        return property.GetCustomAttribute<IgnoreAttribute>() != null;
    }

    /// <summary>
    /// Clears all cached names. Call after changing JauntyConfig resolvers.
    /// </summary>
    public static void ClearCache()
    {
        TableNameCache.Clear();
        ColumnNameCache.Clear();
    }

    private static string ResolveTableName(Type type)
    {
        // 1. Check for TableAttribute
        var attr = type.GetCustomAttribute<TableAttribute>();
        if (attr != null)
            return attr.Name;

        // 2. Check for user-supplied resolver
        var resolver = JauntyConfig.TableNameResolver;
        if (resolver != null)
        {
            var resolved = resolver(type);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }

        // 3. Default to type name
        return type.Name;
    }

    private static string ResolveColumnName(PropertyInfo property)
    {
        // 1. Check for ColumnAttribute
        var attr = property.GetCustomAttribute<ColumnAttribute>();
        if (attr != null)
            return attr.Name;

        // 2. Check for user-supplied resolver
        var resolver = JauntyConfig.ColumnNameResolver;
        if (resolver != null)
        {
            var resolved = resolver(property.Name);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }

        // 3. Default to property name
        return property.Name;
    }
}
