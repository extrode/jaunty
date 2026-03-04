using System.Collections.Concurrent;
using Jaunty.Configuration;
using Jaunty.Internals.Entity;
using Jaunty.Dialects;

namespace Jaunty.Fluent.Internals;

internal static class FluentMetadataCache
{
    private static readonly ConcurrentDictionary<Type, EntityMetadata> _metadataCache = new();
    private static readonly ConcurrentDictionary<(Type, Type), CachedDialectMetadata> _dialectCache = new();

    public static EntityMetadata GetMetadata<T>() where T : new()
    {
        return _metadataCache.GetOrAdd(typeof(T), _ => {
            if (JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata metadata)
            {
                return metadata;
            }

            throw new InvalidOperationException(
                $"No metadata found for type '{typeof(T).Name}'. " +
                "Ensure the class has [Table] for source generation or load 'Jaunty.Extensions.Reflection'.");
        });
    }

    public static CachedDialectMetadata GetForDialect<T>(ISqlDialect dialect) where T : new()
    {
        var key = (typeof(T), dialect.GetType());
        return _dialectCache.GetOrAdd(key, _ => {
            var meta = GetMetadata<T>();
            var escapedTable = dialect.EscapeTableName(meta.SchemaName, meta.TableName);
            var escapedCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var col in meta.Columns)
            {
                escapedCols[col.Property.Name] = dialect.EscapeColumnName(col.ColumnName);
            }

            return new CachedDialectMetadata(escapedTable, escapedCols);
        });
    }
}
