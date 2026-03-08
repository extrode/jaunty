#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Collections.Concurrent;
using System.Reflection;

using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Caches column mappings per entity type to avoid repeated reflection.
/// Uses FrozenDictionary for optimal read performance on .NET 8+.
/// </summary>
internal static class ColumnMappingCache
{
#if NET8_0_OR_GREATER
    private static readonly ConcurrentDictionary<Type, FrozenDictionary<string, ColumnMapping>> _cache = new();
#else
    private static readonly ConcurrentDictionary<Type, Dictionary<string, ColumnMapping>> _cache = new();
#endif

    /// <summary>
    /// Gets or creates column mappings for the specified entity type.
    /// </summary>
    /// <param name="entityType">The entity type to get mappings for.</param>
    /// <returns>A read-only dictionary of column name to mapping.</returns>
    public static IReadOnlyDictionary<string, ColumnMapping> Get(Type entityType)
    {
        return _cache.GetOrAdd(entityType, type =>
        {
            var dict = new Dictionary<string, ColumnMapping>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var columnName = GetColumnName(prop);

                dict[columnName] = new ColumnMapping
                {
                    ColumnName = columnName,
                    Property = prop,
                    Getter = CreateGetter(prop),
                    Setter = CreateSetter(prop),
                    PropertyType = prop.PropertyType,
                    IsDateTime = underlyingType == typeof(DateTime)
                };
            }

#if NET8_0_OR_GREATER
            return dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
#else
            return dict;
#endif
        });
    }

    private static string GetColumnName(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<ColumnAttribute>();
        return attr?.Name ?? prop.Name;
    }

    /// <summary>
    /// Compiles a getter delegate: (object entity) => (object?)entity.Property
    /// </summary>
    private static Func<object, object?> CreateGetter(PropertyInfo prop)
    {
        var param = System.Linq.Expressions.Expression.Parameter(typeof(object), "entity");
        var cast = System.Linq.Expressions.Expression.Convert(param, prop.DeclaringType!);
        var access = System.Linq.Expressions.Expression.Property(cast, prop);
        var box = System.Linq.Expressions.Expression.Convert(access, typeof(object));
        return System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(box, param).Compile();
    }

    /// <summary>
    /// Compiles a setter delegate: (object entity, object? value) => entity.Property = value
    /// </summary>
    private static Action<object, object?> CreateSetter(PropertyInfo prop)
    {
        var entityParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "entity");
        var valueParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "value");
        var cast = System.Linq.Expressions.Expression.Convert(entityParam, prop.DeclaringType!);
        var convertedValue = System.Linq.Expressions.Expression.Convert(valueParam, prop.PropertyType);
        var assign = System.Linq.Expressions.Expression.Assign(System.Linq.Expressions.Expression.Property(cast, prop), convertedValue);
        return System.Linq.Expressions.Expression.Lambda<Action<object, object?>>(assign, entityParam, valueParam).Compile();
    }
}
