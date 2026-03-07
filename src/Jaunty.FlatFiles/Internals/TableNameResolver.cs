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
        var jauntyAttr = entityType.GetCustomAttribute<TableAttribute>();
        if (jauntyAttr is not null)
            return jauntyAttr.Name;

        // Also check System.ComponentModel.DataAnnotations.Schema.TableAttribute for compatibility
        // Avoid LINQ allocation by using for loop instead of FirstOrDefault
        var attrs = entityType.GetCustomAttributes(inherit: false);
        foreach (var attr in attrs)
        {
            var attrType = attr.GetType();
            if (attrType.FullName == "System.ComponentModel.DataAnnotations.Schema.TableAttribute")
            {
                var nameProp = attrType.GetProperty("Name");
                if (nameProp?.GetValue(attr) is string name)
                    return name;
            }
        }

        return entityType.Name.ToLowerInvariant();
    }
}
