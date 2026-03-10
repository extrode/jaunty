using System.Collections.Concurrent;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Caches generated SQL to avoid redundant string building and expression translation.
/// </summary>
internal sealed class SqlCache
{
    private readonly ConcurrentDictionary<CacheKey, string> _cache = new();
    private const int MaxCacheSize = 1000;

    /// <summary>
    /// Gets cached SQL or generates and caches new SQL.
    /// </summary>
    public string GetOrAdd(CacheKey key, Func<string> sqlGenerator)
    {
        if (_cache.TryGetValue(key, out var sql))
            return sql;

        // Prevent unbounded growth - simple clear-on-full strategy
        if (_cache.Count >= MaxCacheSize)
        {
            _cache.Clear();
        }

        sql = sqlGenerator();
        _cache.TryAdd(key, sql);
        return sql;
    }

    /// <summary>
    /// Clears all cached SQL.
    /// </summary>
    public void Clear() => _cache.Clear();
}
