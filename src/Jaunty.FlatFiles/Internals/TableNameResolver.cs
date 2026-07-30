using System.Reflection;

using Jaunty.Attributes;

namespace Jaunty.FlatFiles.Internals;

/// <summary>
/// Resolves the logical table name for an entity type by reading the <see cref="TableAttribute"/>.
/// Falls back to the class name lowercased if no attribute is present.
/// </summary>
internal static class TableNameResolver
{
    /// <summary>
    /// Resolves the table name for the specified entity type.
    /// </summary>
    public static string Resolve<T>() where T : class => Resolve(typeof(T));

    /// <summary>
    /// Resolves the table name for the specified entity type.
    /// </summary>
    public static string Resolve(Type entityType)
    {
        TableAttribute? jauntyAttr = entityType.GetCustomAttribute<TableAttribute>();
        if (jauntyAttr is not null)
            return jauntyAttr.Name;

        // Also check System.ComponentModel.DataAnnotations.Schema.TableAttribute for compatibility
        // Avoid LINQ allocation by using for loop instead of FirstOrDefault
        var attrs = entityType.GetCustomAttributes(inherit: false);
        foreach (var attr in attrs)
        {
#if NET8_0_OR_GREATER
            // Typed check: the attribute lives in the shared framework on net8+, so the duck-typed
            // GetType().GetProperty("Name") read below is unnecessary there - and it is IL2075
            // (an error under net10's stricter trim analyzer): an attribute instance's Type carries
            // no DAM annotations, so the trimmer may remove Name. The typed access keeps it rooted.
            if (attr is System.ComponentModel.DataAnnotations.Schema.TableAttribute schemaAttr)
                return schemaAttr.Name;
#else
            Type attrType = attr.GetType();
            if (attrType.FullName == "System.ComponentModel.DataAnnotations.Schema.TableAttribute")
            {
                PropertyInfo? nameProp = attrType.GetProperty("Name");
                if (nameProp?.GetValue(attr) is string name)
                    return name;
            }
#endif
        }

        return entityType.Name.ToLowerInvariant();
    }
}