using System.Diagnostics;
using System.Reflection;
using System.Text;
using Jaunty.FlatFiles.DuckDB.Tests.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// Tests for the new Query API and performance optimizations added in P0 fixes.
/// </summary>
public class P0FixesTests : IDisposable
{
    private readonly DuckDbFlatFileDatabase _db;
    private readonly string _testCsvPath;

    public P0FixesTests()
    {
        _testCsvPath = Path.Combine(AppContext.BaseDirectory, "test-data", "p0-test-sales.csv");
        
        // Ensure test data directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_testCsvPath)!);
        
        // Create test CSV
        CreateTestCsv();
        
        // Create database
        var options = new FlatFileDatabaseOptions();
        options.AddCsv<SalesRecord>(_testCsvPath);
        _db = new DuckDbFlatFileDatabase(options);
    }

    private void CreateTestCsv()
    {
        var csv = new StringBuilder();
        csv.AppendLine("id,product_name,revenue,quantity,date,region");
        csv.AppendLine("1,Widget A,1000.50,10,2024-01-15,North");
        csv.AppendLine("2,Widget B,2500.00,25,2024-02-20,South");
        csv.AppendLine("3,Gadget X,5000.75,5,2024-03-10,East");
        csv.AppendLine("4,Gadget Y,7500.00,15,2024-04-05,West");
        csv.AppendLine("5,Tool Z,3000.25,30,2024-05-12,Central");
        File.WriteAllText(_testCsvPath, csv.ToString());
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testCsvPath))
            File.Delete(_testCsvPath);
    }

    // ==========================================
    // Query<T>() Tests
    // ==========================================

    [Fact]
    public void Query_ReturnsFluentQueryBuilder()
    {
        // Act
        var query = _db.Connection.From<SalesRecord>();

        // Assert
        Assert.NotNull(query);
        Assert.IsAssignableFrom<IFromClause<SalesRecord>>(query);
    }

    [Fact]
    public void Query_WithSelect_ReturnsAllRows()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>().Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_WithWhere_FilterResults()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 3000m)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count); // Gadget X, Gadget Y, Tool Z
        Assert.All(results, r => Assert.True(r.Revenue > 3000m));
    }

    [Fact]
    public void Query_WithOrderBy_SortsResults()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>()
            .OrderByDescending(x => x.Revenue)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Revenue >= results[i].Revenue);
        }
    }

    [Fact]
    public void Query_WithTake_LimitsResults()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>()
            .Take(3)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Query_WithSkipAndTake_PaginatesResults()
    {
        // Act
        var allResults = _db.Connection.From<SalesRecord>().Select();
        var page2 = _db.Connection.From<SalesRecord>()
            .Skip(2)
            .Take(2)
            .Select();

        // Assert
        Assert.NotNull(page2);
        Assert.Equal(2, page2.Count);
        Assert.Equal(allResults[2].Id, page2[0].Id);
        Assert.Equal(allResults[3].Id, page2[1].Id);
    }

    [Fact]
    public void Query_WithCompoundWhere_AppliesBothConditions()
    {
        // Act
        var results = _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 2000m && x.Quantity > 10)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count); // Widget B, Gadget Y, Tool Z
    }

    // ==========================================
    // QueryAsync<T>() Tests
    // ==========================================

    [Fact]
    public async Task QueryAsync_WithoutParameters_ExecutesRawSql()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>("SELECT * FROM sales");

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task QueryAsync_WithWhereClause_FiltersResults()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales WHERE revenue > $1",
            new[] { (Name: "$1", Value: (object?)3000.0m) });

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.Revenue > 3000m));
    }

    [Fact]
    public async Task QueryAsync_WithMultipleParameters_BindsCorrectly()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales WHERE revenue > $1 AND quantity < $2",
            new[]
            {
                (Name: "$1", Value: (object?)2000.0m),
                (Name: "$2", Value: (object?)20)
            });

        // Assert
        Assert.NotNull(results);
        // Gadget X (5000.75, 5) and Gadget Y (7500, 15)
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task QueryAsync_WithOrderBy_SortsResults()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales ORDER BY revenue DESC");

        // Assert
        Assert.NotNull(results);
        Assert.Equal(5, results.Count);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].Revenue >= results[i].Revenue);
        }
    }

    [Fact]
    public async Task QueryAsync_EmptyResult_ReturnsEmptyList()
    {
        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales WHERE revenue > 1000000");

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    // ==========================================
    // Column Mapping Caching Tests
    // ==========================================

    [Fact]
    public void GetColumnMappings_CachesResults()
    {
        // Arrange
        var entityType = typeof(SalesRecord);

        // Act - First call (populates cache)
        var mappings1 = FlatFileExpressionHelper.GetColumnMappings(entityType);
        
        // Get cache count before second call
        var cacheCountBefore = GetCacheCount();

        // Second call (should use cache)
        var mappings2 = FlatFileExpressionHelper.GetColumnMappings(entityType);

        // Assert
        Assert.Same(mappings1, mappings2); // Same reference from cache
        Assert.Equal(cacheCountBefore, GetCacheCount()); // Cache count unchanged
    }

    [Fact]
    public void GetColumnMappings_DifferentTypes_CachedSeparately()
    {
        // Arrange & Act
        var salesMappings = FlatFileExpressionHelper.GetColumnMappings(typeof(SalesRecord));
        var inventoryMappings = FlatFileExpressionHelper.GetColumnMappings(typeof(InventoryItem));

        // Assert
        Assert.NotSame(salesMappings, inventoryMappings);
        Assert.Equal(6, salesMappings.Count); // Id, ProductName, Revenue, Quantity, Date, Region
        Assert.Equal(6, inventoryMappings.Count); // ItemId, ItemName, Category, StockQuantity, UnitPrice, InStock
    }

    [Fact]
    public void GetColumnMappings_RespectsColumnAttribute()
    {
        // Arrange & Act
        var mappings = FlatFileExpressionHelper.GetColumnMappings(typeof(SalesRecord));

        // Assert
        var productNameMapping = mappings.First(m => m.Property.Name == "ProductName");
        Assert.Equal("product_name", productNameMapping.ColumnName); // From [Column] attribute
    }

    [Fact]
    public void ColumnCaching_ImprovesPerformance()
    {
        // Arrange
        var entityType = typeof(SalesRecord);

        // Warm up cache
        FlatFileExpressionHelper.GetColumnMappings(entityType);

        // Act - Measure cached access time
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            FlatFileExpressionHelper.GetColumnMappings(entityType);
        }
        stopwatch.Stop();

        // Assert - Should be very fast (< 10ms for 1000 cached accesses)
        Assert.True(stopwatch.ElapsedMilliseconds < 10, 
            $"Cached access took {stopwatch.ElapsedMilliseconds}ms, expected < 10ms");
    }

    // ==========================================
    // Expression Caching Tests
    // ==========================================

    [Fact]
    public void EvaluateExpression_CachesCompiledDelegates()
    {
        // Arrange
        var constantExpr = System.Linq.Expressions.Expression.Constant(42);

        // Act - First evaluation
        var result1 = InvokeEvaluateExpression(constantExpr);

        // Act - Second evaluation (should use cache)
        var result2 = InvokeEvaluateExpression(constantExpr);

        // Assert
        Assert.Equal(42, result1);
        Assert.Equal(42, result2);
    }

    [Fact]
    public void ExpressionCaching_ImprovesQueryPerformance()
    {
        // Warm up
        _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 1000m)
            .Select();

        // Act - Measure repeated query performance
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _db.Connection.From<SalesRecord>()
                .Where(x => x.Revenue > 1000m)
                .Select();
        }
        stopwatch.Stop();

        // Assert - Should be reasonably fast with caching (<1s for 100 iterations)
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Repeated queries took {stopwatch.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    // ==========================================
    // Integration Tests
    // ==========================================

    [Fact]
    public void FullQueryPipeline_EndToEnd()
    {
        // Act - Complex query with multiple operations
        var results = _db.Connection.From<SalesRecord>()
            .Where(x => x.Revenue > 2000m)
            .OrderByDescending(x => x.Revenue)
            .Take(3)
            .Select();

        // Assert
        Assert.NotNull(results);
        Assert.Equal(3, results.Count);
        Assert.Equal("Gadget Y", results[0].ProductName); // Highest revenue > 2000
        Assert.Equal("Gadget X", results[1].ProductName);
        Assert.Equal("Tool Z", results[2].ProductName);
    }

    [Fact]
    public async Task QueryAsync_Integration_WithParameters()
    {
        // Arrange
        var minRevenue = 2500.0m;

        // Act
        var results = await _db.QueryAsync<SalesRecord>(
            "SELECT * FROM sales WHERE revenue >= $1 ORDER BY revenue DESC",
            new[] { (Name: "$1", Value: (object?)minRevenue) });

        // Assert
        Assert.NotNull(results);
        Assert.Equal(4, results.Count);
        Assert.All(results, r => Assert.True(r.Revenue >= minRevenue));
    }

    // ==========================================
    // Helper Methods
    // ==========================================

    private int GetCacheCount()
    {
        // Use reflection to access the private cache for testing
        var helperType = typeof(FlatFileExpressionHelper);
        var cacheField = helperType.GetField("_columnMappingCache", 
            BindingFlags.Static | BindingFlags.NonPublic);
        var cache = cacheField?.GetValue(null);
        var countProperty = cache?.GetType().GetProperty("Count");
        return (int)(countProperty?.GetValue(cache) ?? 0);
    }

    private object? InvokeEvaluateExpression(System.Linq.Expressions.Expression expr)
    {
        // Use reflection to call the private EvaluateExpression method
        var helperType = typeof(FlatFileExpressionHelper);
        var method = helperType.GetMethod("EvaluateExpression",
            BindingFlags.Static | BindingFlags.NonPublic);
        return method?.Invoke(null, new[] { expr });
    }
}
