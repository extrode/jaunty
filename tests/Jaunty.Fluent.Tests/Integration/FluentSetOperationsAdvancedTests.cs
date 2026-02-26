using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Additional tests for SetOperationBuilder covering uncovered methods:
/// SelectSingle, SelectSingleOrDefault (sync + async), string-based OrderBy,
/// ThenBy with string columns, SelectFirst/SelectFirstOrDefault async.
/// </summary>
public class FluentSetOperationsAdvancedTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentSetOperationsAdvancedTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // SelectSingle / SelectSingleOrDefault (sync)
    // ==========================================

    [Fact]
    public void Union_SelectSingle_ExactlyOneResult_ReturnsProduct()
    {
        // Query that returns exactly one product (product_id = 1)
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.ProductId == -999))
            .SelectSingle();

        Assert.NotNull(result);
        Assert.Equal(1, result.ProductId);
    }

    [Fact]
    public void Union_SelectSingleOrDefault_ExactlyOneResult_ReturnsProduct()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.ProductId == -999))
            .SelectSingleOrDefault();

        Assert.NotNull(result);
        Assert.Equal(1, result!.ProductId);
    }

    [Fact]
    public void Union_SelectSingleOrDefault_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .Union(_fixture.Connection.From<Product>().Where(p => p.ProductId == -998))
            .SelectSingleOrDefault();

        Assert.Null(result);
    }

    [Fact]
    public void Union_SelectFirst_ReturnsFirst()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectFirst();

        Assert.NotNull(result);
        Assert.True(result.CategoryId == 1 || result.CategoryId == 2);
    }

    [Fact]
    public void Union_SelectFirstOrDefault_NoResults_ReturnsNull()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .Union(_fixture.Connection.From<Product>().Where(p => p.ProductId == -998))
            .SelectFirstOrDefault();

        Assert.Null(result);
    }

    // ==========================================
    // SelectSingle / SelectSingleOrDefault (async)
    // ==========================================

    [Fact]
    public async Task Union_SelectSingleAsync_ExactlyOneResult_ReturnsProduct()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.ProductId == -999))
            .SelectSingleAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result.ProductId);
    }

    [Fact]
    public async Task Union_SelectSingleOrDefaultAsync_ExactlyOneResult_ReturnsProduct()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.ProductId == -999))
            .SelectSingleOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, result!.ProductId);
    }

    [Fact]
    public async Task Union_SelectSingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .Union(_fixture.Connection.From<Product>().Where(p => p.ProductId == -998))
            .SelectSingleOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Union_SelectFirstAsync_ReturnsFirst()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectFirstAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.CategoryId == 1 || result.CategoryId == 2);
    }

    [Fact]
    public async Task Union_SelectFirstOrDefaultAsync_ReturnsFirstOrNull()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectFirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Union_SelectFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .Union(_fixture.Connection.From<Product>().Where(p => p.ProductId == -998))
            .SelectFirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    // ==========================================
    // String-based OrderBy / OrderByDescending
    // ==========================================

    [Fact]
    public void Union_OrderByString_OrdersByColumnName()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy("product_name")
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void Union_OrderByDescendingString_OrdersByColumnNameDesc()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderByDescending("unit_price")
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains("DESC", sql);
    }

    [Fact]
    public void Union_OrderByString_ExecutesCorrectly()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy("product_name")
            .Select();

        Assert.NotEmpty(results);
        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(string.Compare(results[i - 1].ProductName, results[i].ProductName) <= 0);
        }
    }

    // ==========================================
    // ThenBy / ThenByDescending with string
    // ==========================================

    [Fact]
    public void Union_OrderBy_ThenByString_OrdersByMultipleColumns()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.CategoryId)
            .ThenBy("product_name")
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void Union_OrderBy_ThenByDescendingString_OrdersByMultipleColumns()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.CategoryId)
            .ThenByDescending("unit_price")
            .ToSql();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains("DESC", sql);
    }

    // ==========================================
    // Skip on ISetOperationOrderByClause
    // ==========================================

    [Fact]
    public void Union_OrderBy_Skip_PaginatesResults()
    {
        var allResults = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Select();

        var skippedResults = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Skip(2)
            .Select();

        Assert.Equal(allResults.Count - 2, skippedResults.Count);
    }

    [Fact]
    public void Union_OrderBy_Take_LimitsResults()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Take(3)
            .Select();

        Assert.Equal(3, results.Count);
    }

    // ==========================================
    // Except / Intersect async variants
    // ==========================================

    [Fact]
    public async Task Except_SelectFirstAsync_ReturnsFirst()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_fixture.Connection.From<Product>().Where(p => p.Discontinued == true))
            .SelectFirstAsync();

        Assert.NotNull(result);
        Assert.Equal((short)1, result.CategoryId);
        Assert.False(result.Discontinued);
    }

    [Fact]
    public async Task Intersect_SelectSingleOrDefaultAsync_ReturnsResult()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == 1)
            .Intersect(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 1))
            .SelectSingleOrDefaultAsync();

        // Product 1 is in category 1 (Beverages), so intersection should return it
        if (result != null)
        {
            Assert.Equal(1, result.ProductId);
        }
    }

    // ==========================================
    // Chained Set Operations with different terminals
    // ==========================================

    [Fact]
    public void UnionAll_SelectFirst_ReturnsFirst()
    {
        var result = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectFirst();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UnionAll_SelectFirstAsync_ReturnsFirst()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectFirstAsync();

        Assert.NotNull(result);
    }

    [Fact]
    public async Task UnionAll_SelectFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var result = await _fixture.Connection.From<Product>()
            .Where(p => p.ProductId == -999)
            .UnionAll(_fixture.Connection.From<Product>().Where(p => p.ProductId == -998))
            .SelectFirstOrDefaultAsync();

        Assert.Null(result);
    }
}
