using System.Diagnostics;
using System.Reflection;

using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// Tests for ColumnMappingCache performance and caching behavior.
/// </summary>
public class ColumnMappingCachePerformanceTests
{
    [Fact]
    public void Get_CachesResults()
    {
        // Arrange
        var entityType = typeof(SalesRecord);

        // Act - First call (populates cache)
        var mappings1 = ColumnMappingCache.Get(entityType);

        // Second call (should use cache)
        var mappings2 = ColumnMappingCache.Get(entityType);

        // Assert - the same instance came back, which is what "cached" means here.
        Assert.Same(mappings1, mappings2);
    }

    // AUD-R26: this used to also assert that ColumnMappingCache's total entry count was unchanged
    // across the two Get calls. That assertion is racy by construction and was observed failing in
    // a full-suite run while passing in isolation: the count is a *global* static, this project has
    // no xunit.runner.json and so runs collections in parallel, and any other test resolving a
    // different entity type between the two reads changes it. Assert.Same already proves the second
    // call did not rebuild - the count added nothing but a flake, and a suite that cries wolf is
    // worse than one test fewer.

    // Get_DifferentTypes_CachedSeparately and Get_RespectsColumnAttribute were removed from this
    // file - they duplicated ColumnMappingCacheTests.cs's tests of the same name/intent (general
    // caching/mapping correctness, not performance). That file remains the single source of
    // truth for those behaviors; this file keeps only cache-behavior/performance-specific tests.

    [Fact]
    public void Caching_ImprovesPerformance()
    {
        // Wall-clock threshold: a useful canary on a developer machine,
        // pure noise on shared CI runners.
        if (Environment.GetEnvironmentVariable("CI") == "true")
            Assert.Skip("Wall-clock performance thresholds are unreliable on shared CI runners.");

        // Arrange
        var entityType = typeof(SalesRecord);

        // Warm up cache
        ColumnMappingCache.Get(entityType);

        // Act - Measure cached access time
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            ColumnMappingCache.Get(entityType);
        }
        stopwatch.Stop();

        // Assert - Should be very fast (< 10ms for 1000 cached accesses)
        Assert.True(stopwatch.ElapsedMilliseconds < 10,
            $"Cached access took {stopwatch.ElapsedMilliseconds}ms, expected < 10ms");
    }

    private int GetCacheCount()
    {
        // Use reflection to access the private cache for testing
        var cacheType = typeof(ColumnMappingCache);
        var cacheField = cacheType.GetField("_cache",
            BindingFlags.Static | BindingFlags.NonPublic);
        var cache = cacheField?.GetValue(null);
        var countProperty = cache?.GetType().GetProperty("Count");
        return (int)(countProperty?.GetValue(cache) ?? 0);
    }
}