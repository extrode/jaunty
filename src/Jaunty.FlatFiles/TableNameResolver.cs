using System.Reflection;
using Jaunty.Attributes;

namespace Jaunty.FlatFiles;

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
        var dataAnnotationsAttr = entityType.GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().FullName == "System.ComponentModel.DataAnnotations.Schema.TableAttribute");
        if (dataAnnotationsAttr is not null)
        {
            var nameProp = dataAnnotationsAttr.GetType().GetProperty("Name");
            if (nameProp?.GetValue(dataAnnotationsAttr) is string name)
                return name;
        }

        return entityType.Name.ToLowerInvariant();
    }
}
