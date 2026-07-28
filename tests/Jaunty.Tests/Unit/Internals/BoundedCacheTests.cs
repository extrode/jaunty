using Jaunty.Internals.Parameters;

using Xunit;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R25 (B3-2): <c>Evict()</c> runs after every successful insert and its loop condition read
/// <c>ConcurrentDictionary&lt;TKey, TValue&gt;.Count</c>, which is not a field read - it acquires
/// every bucket lock and sums the per-bucket counts, blocking all concurrent writers for the
/// duration.
///
/// <para>
/// The class exists to protect against callers who "embed literals instead of parameters, or
/// generate SQL dynamically", but that is precisely the workload where every call is a cache miss
/// and therefore an insert - so the mitigation serialized exactly the scenario it was written for,
/// on the per-command binding path shared by <c>SqlParameterParserCache</c> and
/// <c>ParameterBinder.TemplateCache</c>.
/// </para>
///
/// <para>
/// The eviction counter is now maintained with <see cref="System.Threading.Interlocked"/>. These
/// tests pin the eviction semantics that change had to preserve exactly.
/// </para>
/// </summary>
public class BoundedCacheTests
{
    [Fact]
    public void TryAdd_BelowTheCap_KeepsEverything()
    {
        var cache = new BoundedCache<string, string>(maxEntries: 10);

        for (int i = 0; i < 10; i++)
            Assert.True(cache.TryAdd($"k{i}", $"v{i}"));

        Assert.Equal(10, cache.Count);
        Assert.True(cache.TryGetValue("k0", out _));
    }

    [Fact]
    public void TryAdd_AboveTheCap_EvictsDownToTheCap()
    {
        var cache = new BoundedCache<string, string>(maxEntries: 4);

        for (int i = 0; i < 12; i++)
            cache.TryAdd($"k{i}", $"v{i}");

        Assert.Equal(4, cache.Count);
    }

    [Fact]
    public void TryAdd_AboveTheCap_EvictsTheOldestFirst()
    {
        var cache = new BoundedCache<string, string>(maxEntries: 3);

        for (int i = 0; i < 6; i++)
            cache.TryAdd($"k{i}", $"v{i}");

        // Least-recently-added goes first, so the last three inserted must be what survives.
        Assert.False(cache.TryGetValue("k0", out _));
        Assert.False(cache.TryGetValue("k2", out _));
        Assert.True(cache.TryGetValue("k3", out _));
        Assert.True(cache.TryGetValue("k5", out _));
    }

    [Fact]
    public void TryAdd_DuplicateKey_DoesNotCountTwice()
    {
        // The counter must track successful inserts, not attempts - otherwise repeated binds of the
        // same SQL would evict live entries.
        var cache = new BoundedCache<string, string>(maxEntries: 5);

        Assert.True(cache.TryAdd("k", "v1"));
        Assert.False(cache.TryAdd("k", "v2"));
        Assert.False(cache.TryAdd("k", "v3"));

        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_ReturnsTheExistingValue_WithoutInsertingAgain()
    {
        var cache = new BoundedCache<string, string>(maxEntries: 5);

        Assert.Equal("first", cache.GetOrAdd("k", _ => "first"));
        Assert.Equal("first", cache.GetOrAdd("k", _ => "second"));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_AboveTheCap_EvictsDownToTheCap()
    {
        var cache = new BoundedCache<string, string>(maxEntries: 4);

        for (int i = 0; i < 20; i++)
            cache.GetOrAdd($"k{i}", k => k);

        Assert.Equal(4, cache.Count);
    }

    [Fact]
    public void Count_StaysAccurateAcrossManyEvictionCycles()
    {
        // The counter and the dictionary must not drift: Count reads the dictionary, the cap check
        // reads the counter, and a drift in either direction either leaks memory or evicts live
        // entries. Several full turnovers of the cap is where drift would show.
        var cache = new BoundedCache<string, string>(maxEntries: 8);

        for (int i = 0; i < 500; i++)
            cache.TryAdd($"k{i}", $"v{i}");

        Assert.Equal(8, cache.Count);
    }

    [Fact]
    public void ConcurrentInserts_ConvergeOnTheCap()
    {
        // The workload the cache was written for and the one the Count-per-insert serialized: every
        // call a distinct key, so every call inserts.
        const int Threads = 8;
        const int PerThread = 500;
        var cache = new BoundedCache<string, string>(maxEntries: 16);

        Parallel.For(0, Threads, t =>
        {
            for (int i = 0; i < PerThread; i++)
                cache.TryAdd($"t{t}-k{i}", "v");
        });

        // Eviction is approximate under concurrency by design, but it must remain bounded rather
        // than growing with the insert count.
        Assert.True(cache.Count <= 16 + Threads, $"expected a bounded size, got {cache.Count}");
        Assert.True(cache.Count > 0);
    }

    [Fact]
    public void ConcurrentGetOrAdd_OnASingleKey_AlwaysReturnsTheWinningValue()
    {
        var cache = new BoundedCache<string, string>(maxEntries: 64);
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();

        Parallel.For(0, 64, i => results.Add(cache.GetOrAdd("shared", _ => "value")));

        Assert.All(results, r => Assert.Equal("value", r));
        Assert.Equal(1, cache.Count);
    }
}
