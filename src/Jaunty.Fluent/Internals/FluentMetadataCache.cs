using System.Collections.Concurrent;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Caches entity metadata, and its per-dialect escaped form, for the fluent builders.
/// </summary>
/// <remarks>
/// AUD-R26 (batch 4). Both caches keyed on the entity type alone, so the first fluent query over a
/// given <c>T</c> fixed its table and column names for the life of the process - even though both
/// come from <c>JauntyConfig</c>'s name resolvers and <c>ReflectionTableMetadataResolver</c>, which
/// are public and settable at any time. <see cref="ConfigurationGeneration"/> retires an entry
/// whose configuration has moved; see that type for why deferring the read per call, the fix used
/// elsewhere in this area, cannot work here.
/// </remarks>
internal static class FluentMetadataCache
{
    private static readonly ConcurrentDictionary<Type, ConfigurationScoped<EntityMetadata>> _metadataCache = new();
    private static readonly ConcurrentDictionary<(Type, Type), ConfigurationScoped<CachedDialectMetadata>> _dialectCache = new();

    public static EntityMetadata GetMetadata<T>() where T : new()
    {
        // Read the generation before the lookup, never after: see ConfigurationGeneration.Current.
        int generation = ConfigurationGeneration.Current;

        if (_metadataCache.TryGetValue(typeof(T), out ConfigurationScoped<EntityMetadata> cached) && cached.Generation == generation)
            return cached.Value;

        EntityMetadata metadata =
            SourceGeneratedMetadataResolver.TryBuild<T>() is EntityMetadata sourceGenMetadata
                ? sourceGenMetadata
                : JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata resolved
                    ? resolved
                    : throw new InvalidOperationException(
                        $"No metadata found for type '{typeof(T).Name}'. " +
                        "Ensure the class has [Table] and is processed by the Jaunty source generator " +
                        "(the class must be declared 'partial'), or call " +
                        "Jaunty.Extensions.Reflection's UseReflectionMapping().");

        _metadataCache[typeof(T)] = new ConfigurationScoped<EntityMetadata>(generation, metadata);
        return metadata;
    }

    public static CachedDialectMetadata GetForDialect<T>(ISqlDialect dialect) where T : new()
    {
        (Type, Type) key = (typeof(T), dialect.GetType());

        int generation = ConfigurationGeneration.Current;

        if (_dialectCache.TryGetValue(key, out ConfigurationScoped<CachedDialectMetadata> cached) && cached.Generation == generation)
            return cached.Value;

        EntityMetadata meta = GetMetadata<T>();
        string escapedTable = dialect.EscapeTableName(meta.SchemaName, meta.TableName);
        var escapedCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rawCols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (ColumnMetadata col in meta.Columns)
        {
            escapedCols[col.PropertyName] = dialect.EscapeColumnName(col.ColumnName);
            rawCols[col.PropertyName] = col.ColumnName;
        }

        var dialectMetadata = new CachedDialectMetadata(escapedTable, escapedCols, rawCols, dialect);
        _dialectCache[key] = new ConfigurationScoped<CachedDialectMetadata>(generation, dialectMetadata);
        return dialectMetadata;
    }
}
