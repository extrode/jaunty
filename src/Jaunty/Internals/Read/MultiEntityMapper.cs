using System.Data;

using Jaunty.Configuration;
using Jaunty.Internals;

namespace Jaunty.Internals.Read;

/// <summary>
/// Hook for multi-entity mapping. Actual implementation resides in extensions or source-gen.
/// </summary>
internal sealed class MultiEntityMapper<T1, T2> where T1 : new() where T2 : new()
{
    // Keyed by reader column layout (field count + names), not just (typeof(T1), typeof(T2)) -
    // mirrors MultiEntityMapperN.cs's arity-3..7 BuildSchemaKey rationale. Since this class is
    // already generic on T1/T2, a static field's typeof(T1)/typeof(T2) never vary per
    // instantiation, so keying on them alone amounted to a single-entry cache: two different
    // multi-entity queries projecting into the same (T1, T2) pair but splitting columns
    // differently would silently reuse the first query's cached split points.
    //
    // AUD-R26-053: bounded. The key is the result set's column-name list, so it is caller-controlled
    // through the SELECT list, and this was a ConcurrentDictionary that nothing ever removed from -
    // a permanent entry per distinct shape, for the process lifetime. See
    // BoundedCache.SchemaCacheMaxEntries for why the cap is 256 rather than the 4096 the two
    // parameter caches use.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2>> _cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

    private readonly Action<T1, T2, IDataRecord> _map;

    private MultiEntityMapper(Action<T1, T2, IDataRecord> map) => _map = map;

    internal static MultiEntityMapper<T1, T2> Build(IDataReader reader)
    {
        string key = BuildSchemaKey(reader);

        MultiEntityMapper<T1, T2>? cached = _cache.Get(key);
        if (cached is not null)
            return cached;

        MultiEntityMapper<T1, T2> mapper = CreateMapper(reader);
        _cache.TryAdd(key, mapper);
        return mapper;
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        int fieldCount = reader.FieldCount;
        var parts = new string[fieldCount + 1];
        parts[0] = fieldCount.ToString();
        for (int i = 0; i < fieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    /// <remarks>
    /// AUD-R26. This used to split the resolver's combined mapper into two one-entity closures -
    /// <c>(t1, r) =&gt; combined(t1, default!, r)</c> and <c>(t2, r) =&gt; combined(default!, t2, r)</c> -
    /// which every arity-2 call site then invoked in sequence, once per row. Two problems fell out
    /// of that, and both disappear by handing the combined mapper straight through.
    ///
    /// <para>
    /// The reflection side's combined delegate starts with <c>MultiEntityMapper&lt;T1, T2&gt;.Get(reader)</c>,
    /// whose per-reader memoization exists precisely because it is called per row - and every hit is
    /// validated by <c>ReaderCacheEntry.Matches</c>, which compares <c>reader.GetName(i)</c> across
    /// the full field count. Calling it twice per row doubled the one cost that memoization was
    /// added to remove: a 20-column join over 10k rows paid 20k lookups and 400k string comparisons
    /// where 10k and 200k would do. The N-ary path never had this shape; only arity 2 paid double.
    /// </para>
    ///
    /// <para>
    /// The <c>default!</c> arguments were the second problem. The reflection side's <c>Map</c>
    /// null-guards each target, so for a reference type the discarded half was skipped - but
    /// <c>T1</c>/<c>T2</c> are only constrained to <c>new()</c>, and for a value-type entity
    /// <c>default</c> is a zeroed struct, not null. That is not null, so <c>Map</c> ran that type's
    /// full setter list against a boxed throwaway copy and dropped it: work done, twice per row,
    /// with no observable effect.
    /// </para>
    /// </remarks>
    // The reader parameter is deliberately unread (R27 CF-3): it exists so the caller's cache key
    // can be the reader's column list, matching the arity-3..7 siblings' signature; the resolver
    // itself keys on the type pair alone.
    private static MultiEntityMapper<T1, T2> CreateMapper(IDataReader reader)
    {
        // AUD-R34-015: a struct entity is passed by value to the combined delegate, so its setters
        // write to a copy and the caller gets an all-default entity back. Same rejection as the
        // N-ary path - see MultiEntityMapperNGuard.RequireReferenceTypes.
        MultiEntityMapperNGuard.RequireReferenceTypes(new[] { typeof(T1), typeof(T2) });

        if (JauntyConfig.ReflectionMultiMapperResolver?.Invoke(typeof(T1), typeof(T2)) is Action<T1, T2, IDataRecord> combined)
            return new MultiEntityMapper<T1, T2>(combined);

        throw new InvalidOperationException(
            $"No multi-mapper found for types '{typeof(T1).Name}' and '{typeof(T2).Name}'. " +
            "Ensure 'Jaunty.Extensions.Reflection' is loaded for runtime multi-mapping.");
    }

    /// <summary>Populates both targets from one row, in a single pass over the resolved mapper.</summary>
    internal void Map(T1 target1, T2 target2, IDataRecord record) => _map(target1, target2, record);
}