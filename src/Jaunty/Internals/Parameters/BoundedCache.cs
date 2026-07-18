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

    internal int Count => _entries.Count;

    internal bool TryGetValue(TKey key, out TValue? value) => _entries.TryGetValue(key, out value);

    internal bool TryAdd(TKey key, TValue value)
    {
        if (_entries.TryAdd(key, value))
        {
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
        while (_entries.Count > _maxEntries && _insertionOrder.TryDequeue(out TKey? oldest))
        {
            if (oldest is not null)
                _entries.TryRemove(oldest, out _);
        }
    }
}
