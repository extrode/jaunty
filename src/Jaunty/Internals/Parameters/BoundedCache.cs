using System.Collections.Concurrent;

namespace Jaunty.Internals.Parameters;

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
