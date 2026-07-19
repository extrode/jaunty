using System.Collections.Concurrent;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Internals;

internal static class FluentMetadataCache
{
    private static readonly ConcurrentDictionary<Type, EntityMetadata> _metadataCache = new();
    private static readonly ConcurrentDictionary<(Type, Type), CachedDialectMetadata> _dialectCache = new();

    public static EntityMetadata GetMetadata<T>() where T : new()
    {
        return _metadataCache.GetOrAdd(typeof(T), _ =>
        {
            return SourceGeneratedMetadataResolver.TryBuild<T>() is EntityMetadata sourceGenMetadata
                ? sourceGenMetadata
                : JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata metadata
                    ? metadata
                    : throw new InvalidOperationException(
                    $"No metadata found for type '{typeof(T).Name}'. " +
                    "Ensure the class has [Table] and is processed by the Jaunty source generator " +
                    "(the class must be declared 'partial'), or call " +
                    "Jaunty.Extensions.Reflection's UseReflectionMapping().");
        });
    }

    public static CachedDialectMetadata GetForDialect<T>(ISqlDialect dialect) where T : new()
    {
        (Type, Type) key = (typeof(T), dialect.GetType());
        return _dialectCache.GetOrAdd(key, _ =>
        {
            EntityMetadata meta = GetMetadata<T>();
            string escapedTable = dialect.EscapeTableName(meta.SchemaName, meta.TableName);
            var escapedCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (ColumnMetadata col in meta.Columns)
            {
                escapedCols[col.PropertyName] = dialect.EscapeColumnName(col.ColumnName);
            }

            return new CachedDialectMetadata(escapedTable, escapedCols);
        });
    }
}