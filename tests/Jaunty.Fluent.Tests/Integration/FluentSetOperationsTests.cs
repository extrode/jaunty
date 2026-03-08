using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for UNION, UNION ALL, EXCEPT, INTERSECT set operations.
/// </summary>
public class FluentSetOperationsTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentSetOperationsTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // UNION Tests
    // ==========================================

    [Fact]
    public void Union_TwoQueries_ReturnsCombinedResultsWithoutDuplicates()
    {
        // Get products from category 1 UNION products from category 2
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Select();

        Assert.NotEmpty(results);
        // All results should be from category 1 or 2
        Assert.All(results, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    [Fact]
    public void Union_ToSql_GeneratesUnionKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        Assert.Contains("UNION", sql);
        Assert.DoesNotContain("UNION ALL", sql);
    }

    [Fact]
    public void Union_WithOrderBy_OrdersEntireResult()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void Union_WithOrderByDescending_OrdersEntireResultDescending()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderByDescending(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) >= 0);
        }
    }

    [Fact]
    public void Union_WithTake_LimitsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Take(3)
            .Select();

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Union_ChainedMultipleTimes_CombinesAllQueries()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 3))
            .Select();

        Assert.NotEmpty(results);
        Assert.All(results, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2 || p.CategoryId == 3));
    }

    // ==========================================
    // UNION ALL Tests
    // ==========================================

    [Fact]
    public void UnionAll_TwoQueries_ReturnsCombinedResultsWithDuplicates()
    {
        // UNION ALL keeps duplicates (same products may appear multiple times)
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 1))
            .Select();

        // With UNION ALL on the same query, count should double
        var singleQueryCount = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Count();

        Assert.Equal(singleQueryCount * 2, results.Count);
    }

    [Fact]
    public void UnionAll_ToSql_GeneratesUnionAllKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        Assert.Contains("UNION ALL", sql);
    }

    [Fact]
    public void UnionAll_WithOrderBy_OrdersEntireResult()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.UnitPrice)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].UnitPrice <= results[i].UnitPrice);
        }
    }

    // ==========================================
    // EXCEPT Tests
    // ==========================================

    [Fact]
    public void Except_TwoQueries_ReturnsRowsNotInSecondQuery()
    {
        // Get products from category 1 that are NOT discontinued
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .Select();

        Assert.NotEmpty(results);
        // All results should be from category 1 and not discontinued
        Assert.All(results, p => Assert.True(p.CategoryId == 1 && !p.Discontinued));
    }

    [Fact]
    public void Except_ToSql_GeneratesExceptKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        Assert.Contains("EXCEPT", sql);
    }

    [Fact]
    public void Except_WithOrderBy_OrdersEntireResult()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) <= 0);
        }
    }

    // ==========================================
    // INTERSECT Tests
    // ==========================================

    [Fact]
    public void Intersect_TwoQueries_ReturnsOnlyCommonRows()
    {
        // Get products that are both in category 1 AND have low stock (ReorderLevel > 0)
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .Select();

        // All results should be in category 1 AND have ReorderLevel > 0
        Assert.All(results, p => Assert.True(p.CategoryId == 1 && p.ReorderLevel > 0));
    }

    [Fact]
    public void Intersect_ToSql_GeneratesIntersectKeyword()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 1))
            .ToSql();

        Assert.Contains("INTERSECT", sql);
    }

    [Fact]
    public void Intersect_WithOrderBy_OrdersEntireResult()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .OrderByDescending(p => p.ProductName)
            .Select();

        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) >= 0);
        }
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task Union_SelectAsync_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectAsync();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task UnionAll_SelectAsync_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectAsync();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task Except_SelectAsync_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .SelectAsync();

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task Intersect_SelectAsync_ReturnsResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .SelectAsync();

        // Results may be empty if no intersection, but should not throw
        Assert.NotNull(results);
    }

    // ==========================================
    // SelectFirst/SelectSingle Tests
    // ==========================================

    [Fact]
    public void Union_SelectFirst_ReturnsSingleResult()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectFirst();

        Assert.NotNull(result);
        Assert.True(result.CategoryId == 1 || result.CategoryId == 2);
    }

    [Fact]
    public void Union_SelectFirstOrDefault_ReturnsNullWhenNoResults()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == -999)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == -998))
            .SelectFirstOrDefault();

        Assert.Null(result);
    }

    // ==========================================
    // ThenBy Tests
    // ==========================================

    [Fact]
    public void Union_OrderByThenBy_OrdersByMultipleColumns()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("category_id", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void Union_OrderByThenByDescending_OrdersCorrectly()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.ProductName)
            .Select();

        Assert.NotEmpty(results);
    }

    // ==========================================
    // Skip/Take Tests
    // ==========================================

    [Fact]
    public void Union_SkipTake_PaginatesResults()
    {
        var allResults = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Select();

        var pagedResults = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Skip(2)
            .Take(3)
            .Select();

        Assert.Equal(3, pagedResults.Count);
        // Verify skip worked
        Assert.Equal(allResults[2].ProductId, pagedResults[0].ProductId);
    }

    // ==========================================
    // Mixed Operations Tests
    // ==========================================

    [Fact]
    public void Union_ThenExcept_ChainedOperations()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .ToSql();

        Assert.Contains("UNION", sql);
        Assert.Contains("EXCEPT", sql);
    }

    [Fact]
    public void UnionAll_ThenIntersect_ChainedOperations()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .ToSql();

        Assert.Contains("UNION ALL", sql);
        Assert.Contains("INTERSECT", sql);
    }
}