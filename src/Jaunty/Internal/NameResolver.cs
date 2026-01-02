using System.Collections.Concurrent;
using System.Reflection;

using Jaunty.Attributes;
using Jaunty.Configuration;

namespace Jaunty.Internal;

internal static class NameResolver
{
    private static readonly ConcurrentDictionary<Type, string> TableNameCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, string> ColumnNameCache = new();
    private static readonly ConcurrentDictionary<PropertyInfo, bool> IgnoredCache = new();

    public static string GetTableName(Type type)
    {
        return TableNameCache.GetOrAdd(type, ResolveTableName);
    }

    public static string GetColumnName(PropertyInfo property)
    {
        return ColumnNameCache.GetOrAdd(property, ResolveColumnName);
    }

    public static bool IsIgnored(PropertyInfo property)
    {
        return IgnoredCache.GetOrAdd(property, p => p.IsDefined(typeof(IgnoreAttribute), false));
    }

    public static void ClearCache()
    {
        TableNameCache.Clear();
        ColumnNameCache.Clear();
        IgnoredCache.Clear();
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
