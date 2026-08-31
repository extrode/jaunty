using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Internals;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Maps multiple entity types from a single data reader using reflection.
/// </summary>
/// <remarks>
/// This mapper uses runtime reflection and is not compatible with NativeAOT.
/// For NativeAOT scenarios, use the source generator instead.
/// </remarks>
internal sealed class MultiEntityMapper<T1, T2> where T1 : new() where T2 : new()
{
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    // AUD-R35-108 (round-35 batch 04a): StringComparer.Ordinal, matching the core mappers in
    // src/Jaunty/Internals/Read. This side used OrdinalIgnoreCase, with neither side saying why,
    // so two result sets differing only in column-name casing shared one cached mapper here and
    // got two entries there. Immaterial either way - the mapper binds by the ordinal position of
    // the key's column list - but the cache key is a contract, and Ordinal is the one that never
    // merges two schemas a provider would call distinct.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2>> Cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

    // Per-reader-instance memoization: GetTypedMultiMapper's delegate calls Get(reader) on every
    // row of a result set, and the same IDataReader instance is passed for every row of that
    // set. Without this, each row paid the schema-key string allocation/join even on a cache
    // hit; a ConditionalWeakTable keyed by the reader object gives an O(1) hit from the second
    // row onward while still going through the schema-key Cache (and thus reusing mappers
    // across different reader instances with the same column shape) on the first row.
    //
    // Some providers (e.g. Npgsql) recycle a single IDataReader instance across different
    // commands executed on the same pooled physical connection, so a cache hit on reader
    // identity alone can silently return a mapper built for a completely different column
    // layout (AUD-R9-011 regression from bac03a3's original ConditionalWeakTable<IDataReader,
    // MultiEntityMapper<T1,T2>> - a stale mapper bound "Beverages" to an int CategoryId
    // property). Every hit is therefore validated against the reader's current schema before
    // being trusted, same as the source generator's OrdinalMap.CacheEntry.Matches pattern.
    private static readonly ConditionalWeakTable<IDataReader, ReaderCacheEntry> ReaderCache = new();
#if !NET8_0_OR_GREATER
    private static readonly object ReaderCacheWriteLock = new();
#endif

    private readonly PropertySetter<T1>[] _t1Setters;
    private readonly PropertySetter<T2>[] _t2Setters;

    private MultiEntityMapper(PropertySetter<T1>[] t1Setters, PropertySetter<T2>[] t2Setters)
    {
        _t1Setters = t1Setters;
        _t2Setters = t2Setters;
    }

    public static MultiEntityMapper<T1, T2> Get(IDataReader reader)
    {
        if (ReaderCache.TryGetValue(reader, out ReaderCacheEntry? entry) && entry.Matches(reader))
            return entry.Mapper;

        string schemaKey = BuildSchemaKey(reader);
        MultiEntityMapper<T1, T2> mapper = Cache.GetOrAdd(schemaKey, _ => Create(reader));

        // Not Remove-then-Add: ConditionalWeakTable.Add throws ArgumentException when the key is
        // already present, so that two-step form races with itself - two threads both miss, both
        // remove, both add, and the second Add throws. Same fix as MetadataCache.GetSetters.
        var freshEntry = new ReaderCacheEntry(reader, mapper);
#if NET8_0_OR_GREATER
        ReaderCache.AddOrUpdate(reader, freshEntry);
#else
        lock (ReaderCacheWriteLock)
        {
            ReaderCache.Remove(reader);
            ReaderCache.Add(reader, freshEntry);
        }
#endif

        return mapper;
    }

    private sealed class ReaderCacheEntry
    {
        private readonly int _fieldCount;
        private readonly string[] _columnNames;

        // AUD-R34-023: the per-reader memo short-circuits the schema-key Cache entirely, so it
        // needs the same generation tag - otherwise a configuration change was invisible for as
        // long as the provider kept handing back the same reader instance.
        private readonly int _generation;

        public ReaderCacheEntry(IDataReader reader, MultiEntityMapper<T1, T2> mapper)
        {
            _generation = ConfigurationGeneration.Current;
            _fieldCount = reader.FieldCount;
            _columnNames = new string[_fieldCount];
            for (int i = 0; i < _fieldCount; i++)
                _columnNames[i] = reader.GetName(i) ?? string.Empty;
            Mapper = mapper;
        }

        public MultiEntityMapper<T1, T2> Mapper { get; }

        public bool Matches(IDataReader reader)
        {
            if (_generation != ConfigurationGeneration.Current)
                return false;

            if (reader.FieldCount != _fieldCount)
                return false;

            for (int i = 0; i < _fieldCount; i++)
            {
                if (!string.Equals(reader.GetName(i), _columnNames[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }

    /// <summary>Alias for Get — builds or retrieves a cached mapper for the reader schema.</summary>
    public static MultiEntityMapper<T1, T2> Build(IDataReader reader) => Get(reader);

    private static string BuildSchemaKey(IDataReader reader)
    {
        // AUD-R34-023: the generation is part of the key. Without it a JauntyConfig.ColumnNameResolver
        // change (or JauntyConfig.Reset(), both of which call ConfigurationGeneration.Invalidate())
        // left an already-built mapper in place for the process lifetime - MetadataCache<T> below
        // rebuilt and JauntyReflectionExtensions.MultiMapperCache above is ConfigurationScoped, but
        // the delegate that cache rebuilds calls straight back into this one, which returned the
        // pre-change mapper for a column shape it had seen before.
        string[] parts = new string[reader.FieldCount + 1];
        parts[0] = ConfigurationGeneration.Current.ToString() + "|" + reader.FieldCount.ToString();

        for (int i = 0; i < reader.FieldCount; i++)
            parts[i + 1] = reader.GetName(i) ?? string.Empty;

        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2> Create(IDataReader reader)
    {
        // Build setter arrays using MetadataCache for each type.
        // Using Projection mode to allow columns to be missing if they match the other type.
        PropertySetter<T1>[] t1Setters = MetadataCache<T1>.GetSetters(reader, MappingMode.Projection);

        // T1 has priority: exclude from T2 any ordinals already claimed by T1, and rebind
        // any T2 property whose column name is shared with T1 to the next unclaimed
        // occurrence of that column name (left-to-right ordinal claiming), via the same
        // shared algorithm used by the arity 3-7 mappers.
        var t1Ordinals = new HashSet<int>();

        for (int i = 0; i < t1Setters.Length; i++)
            t1Ordinals.Add(t1Setters[i].Ordinal);

        (PropertySetter<T2>[] t2Setters, _) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, t1Ordinals);

        return new MultiEntityMapper<T1, T2>(t1Setters, t2Setters);
    }

    public void Map(T1? t1, T2? t2, IDataRecord record)
    {
        if (t1 is not null) ApplyT1(t1, record);
        if (t2 is not null) ApplyT2(t2, record);
    }

    public void ApplyT1(T1 target, IDataRecord record)
    {
        for (int i = 0; i < _t1Setters.Length; i++)
            _t1Setters[i].Set(target, record);
    }

    public void ApplyT2(T2 target, IDataRecord record)
    {
        for (int i = 0; i < _t2Setters.Length; i++)
            _t2Setters[i].Set(target, record);
    }
}