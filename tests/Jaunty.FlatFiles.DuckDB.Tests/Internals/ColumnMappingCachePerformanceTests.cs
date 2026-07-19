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

        // Get cache count before second call
        var cacheCountBefore = GetCacheCount();

        // Second call (should use cache)
        var mappings2 = ColumnMappingCache.Get(entityType);

        // Assert
        Assert.Same(mappings1, mappings2); // Same reference from cache
        Assert.Equal(cacheCountBefore, GetCacheCount()); // Cache count unchanged
    }

    [Fact]
    public void Get_DifferentTypes_CachedSeparately()
    {
        // Arrange & Act
        var salesMappings = ColumnMappingCache.Get(typeof(SalesRecord));
        var inventoryMappings = ColumnMappingCache.Get(typeof(InventoryItem));

        // Assert
        Assert.NotSame(salesMappings, inventoryMappings);
        Assert.Equal(6, salesMappings.Count); // Id, ProductName, Revenue, Quantity, Date, Region
        Assert.Equal(6, inventoryMappings.Count); // ItemId, ItemName, Category, StockQuantity, UnitPrice, InStock
    }

    [Fact]
    public void Get_RespectsColumnAttribute()
    {
        // Arrange & Act
        var mappings = ColumnMappingCache.Get(typeof(SalesRecord));

        // Assert
        var productNameMapping = mappings.First(m => m.Value.Property.Name == "ProductName");
        Assert.Equal("product_name", productNameMapping.Value.ColumnName); // From [Column] attribute
    }

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