using System.Collections.Concurrent;

namespace Jaunty.Internals;

/// <summary>
/// Caps for <see cref="BoundedCache{TKey, TValue}"/>, on a non-generic type so a call site can name
/// one without first naming a closed <c>BoundedCache</c>.
/// </summary>
internal static class BoundedCacheLimits
{
    /// <summary>
    /// The cap for caches keyed by a result set's column layout - the multi-entity mappers in
    /// <c>Internals/Read</c> and the mapper and setter caches in Jaunty.Extensions.Reflection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately far below <see cref="BoundedCache{TKey, TValue}.DefaultMaxEntries"/>. That cap
    /// was chosen for the two process-wide singletons in <c>Internals/Parameters</c>, where 4096
    /// entries is 4096 entries full stop. The schema caches are <c>static</c> fields on
    /// <em>generic</em> types, so there is one cache per closed generic instantiation - per entity
    /// type, and per ordered tuple of entity types for the multi-entity mappers. The same numeric
    /// cap therefore multiplies by the application's type surface rather than standing alone, and
    /// AUD-R26-053 measured 586 B per setter-cache entry and 2,047 B per arity-2 mapper entry: 4096
    /// would have permitted roughly 2.4 MB per entity type and 8.4 MB per mapper tuple.
    /// </para>
    /// <para>
    /// 256 distinct column layouts for one entity type is already well past any hand-written query
    /// set; a workload that exceeds it is generating SELECT lists, which is the case the cap exists
    /// for. Eviction is cheap here - a miss rebuilds a <c>PropertySetter</c> array from metadata
    /// that is itself cached elsewhere, and compiles nothing, since the expression trees are built
    /// once per entity type in <c>MetadataCache&lt;T&gt;.Snapshot</c>'s constructor. Nor does
    /// row-by-row mapping reach these caches after the first row: the single-entity and arity-2
    /// paths both memoize on reader identity in front of them.
    /// </para>
    /// </remarks>
    internal const int SchemaCacheMaxEntries = 256;
}

/// <summary>
/// A size-capped, thread-safe cache. Wraps a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// and evicts the oldest (least-recently-added) entries once the configured maximum is exceeded.
/// This prevents unbounded growth when callers embed literals instead of parameters, or generate
/// SQL dynamically, which would otherwise leak memory through an ever-growing key set.
/// NativeAOT-safe: no reflection, no LINQ, no boxing.
/// </summary>
internal sealed class BoundedCache<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    // Default cap. Large enough that realistic parameterized workloads never evict,
    // small enough that pathological dynamic-SQL callers cannot exhaust memory.
    internal const int DefaultMaxEntries = 4096;

    private readonly ConcurrentDictionary<TKey, TValue> _entries;
    private readonly ConcurrentQueue<TKey> _insertionOrder = new();
    private readonly int _maxEntries;

    // AUD-R25: Evict used to loop on _entries.Count. ConcurrentDictionary.Count is not a field read
    // - it acquires every bucket lock and sums the per-bucket counts, blocking all concurrent
    // writers for the duration - and Evict runs after every successful insert. That serialized
    // exactly the workload this cache exists to protect against, since callers who generate SQL
    // dynamically miss on every lookup and therefore insert on every call. An Interlocked counter is
    // approximate under concurrency, which is fine for a cap: the dictionary and the insertion queue
    // remain the source of truth for what is actually stored and evicted.
    private int _count;

    internal BoundedCache(int maxEntries = DefaultMaxEntries)
    {
        _maxEntries = maxEntries > 0 ? maxEntries : DefaultMaxEntries;
        _entries = new ConcurrentDictionary<TKey, TValue>();
    }

    internal BoundedCache(IEqualityComparer<TKey> comparer, int maxEntries = DefaultMaxEntries)
    {
        _maxEntries = maxEntries > 0 ? maxEntries : DefaultMaxEntries;
        _entries = new ConcurrentDictionary<TKey, TValue>(comparer);
    }

    /// <summary>
    /// The number of cached entries. Exact - reads the dictionary rather than the eviction counter,
    /// since callers of this property want the true size, not the cap-tracking approximation.
    /// </summary>
    internal int Count => _entries.Count;

    internal bool TryGetValue(TKey key, out TValue? value) => _entries.TryGetValue(key, out value);

    /// <summary>
    /// The cached value, or <see langword="null"/> if the key is absent.
    /// </summary>
    /// <remarks>
    /// The <c>Try</c> form's <c>out</c> parameter has to be declared <c>TValue?</c>, because this
    /// assembly targets netstandard2.0 where <c>MaybeNullWhenAttribute</c> does not exist and so the
    /// compiler cannot be told the value is non-null on <see langword="true"/>. Callers that return
    /// the result therefore hit CS8603 and have to either suppress it or re-check a null that
    /// <c>TValue : class</c> plus "nothing ever stores null" already rules out. Returning the value
    /// directly makes the flow analysis correct with nothing to suppress, and unlike
    /// <see cref="GetOrAdd"/> it allocates no factory delegate on the hit path - which matters, as
    /// these are lookups on the per-result-set mapping path.
    /// </remarks>
    internal TValue? Get(TKey key) => _entries.TryGetValue(key, out TValue? value) ? value : null;

    internal bool TryAdd(TKey key, TValue value)
    {
        if (_entries.TryAdd(key, value))
        {
            Interlocked.Increment(ref _count);
            _insertionOrder.Enqueue(key);
            Evict();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds the value, or overwrites the one already stored under <paramref name="key"/>.
    /// </summary>
    /// <remarks>
    /// AUD-R35-058. For caches whose entries carry a <c>ConfigurationGeneration</c> stamp, a stale
    /// entry has to be replaced rather than left in place, which <see cref="TryAdd"/> cannot do and
    /// <see cref="GetOrAdd"/> would silently decline to do. An overwrite does not re-enqueue the
    /// key: the entry is the same one, so its position in the eviction order is unchanged, and
    /// enqueuing again would let one repeatedly-refreshed key push others out.
    /// </remarks>
    internal void Set(TKey key, TValue value)
    {
        if (_entries.TryAdd(key, value))
        {
            Interlocked.Increment(ref _count);
            _insertionOrder.Enqueue(key);
            Evict();
            return;
        }

        _entries[key] = value;
    }

    internal TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
    {
        if (_entries.TryGetValue(key, out TValue? existing))
            return existing;

        TValue created = factory(key);
        if (_entries.TryAdd(key, created))
        {
            Interlocked.Increment(ref _count);
            _insertionOrder.Enqueue(key);
            Evict();
            return created;
        }

        // Lost the race: another thread inserted first. Return the winning value.
        if (_entries.TryGetValue(key, out TValue? winner))
            return winner;

        return created;
    }

    private void Evict()
    {
        while (Volatile.Read(ref _count) > _maxEntries && _insertionOrder.TryDequeue(out TKey? oldest))
        {
            // Only a successful removal decrements. Evict is the sole remover, so TryRemove failing
            // means the key was already gone and the counter already reflects that; the loop still
            // terminates because the queue is finite and shrinks on every iteration.
            if (oldest is not null && _entries.TryRemove(oldest, out _))
                Interlocked.Decrement(ref _count);
        }
    }
}
