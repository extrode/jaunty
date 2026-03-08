using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentGroupByTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentGroupByTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // --- Basic GROUP BY Tests ---

    [Fact]
    public void GroupBy_SingleKey_WithCount_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > 0));
    }

    [Fact]
    public void GroupBy_SingleKey_WithSum_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, TotalStock = g.Sum(p => p.UnitsInStock) });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void GroupBy_SingleKey_WithAvg_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, AvgStock = g.Avg(p => p.UnitsInStock) });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void GroupBy_SingleKey_WithMin_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, MinStock = g.Min(p => p.UnitsInStock) });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void GroupBy_SingleKey_WithMax_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, MaxStock = g.Max(p => p.UnitsInStock) });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void GroupBy_SingleKey_MultipleAggregates_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                ProductCount = g.Count(),
                TotalStock = g.Sum(p => p.UnitsInStock),
                AvgStock = g.Avg(p => p.UnitsInStock)
            });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.ProductCount > 0));
    }

    // --- GROUP BY with WHERE Tests ---

    [Fact]
    public void GroupBy_WithWhere_FiltersBeforeGrouping()
    {
        // Group only non-discontinued products
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);

        // Verify count is less than total without filter
        var totalCount = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .Count();

        Assert.Equal(totalCount, results.Sum(r => r.Count));
    }

    [Fact]
    public void GroupBy_WithWhereOnCategoryId_FiltersCorrectly()
    {
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
    }

    // --- GROUP BY with HAVING Tests ---

    [Fact]
    public void GroupBy_WithHaving_FiltersGroupsAfterGrouping()
    {
        // Only return groups with more than 5 products
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > 5)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.All(results, r => Assert.True(r.Count > 5));
    }

    [Fact]
    public void GroupBy_WithHaving_CountGreaterThanZero_ReturnsNonEmptyGroups()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > 0)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > 0));
    }

    // --- GROUP BY with Composite Key Tests ---

    [Fact]
    public void GroupBy_CompositeKey_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => new { p.CategoryId, p.SupplierId })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.SupplierId,
                Count = g.Count()
            });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void GroupBy_CompositeKey_WithAggregates_ReturnsGroupedResults()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => new { p.CategoryId, p.SupplierId })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.SupplierId,
                ProductCount = g.Count(),
                TotalStock = g.Sum(p => p.UnitsInStock)
            });

        Assert.NotEmpty(results);
    }

    // --- ToSql Tests ---

    [Fact]
    public void ToSql_SimpleGroupBy_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Contains("SELECT", sql);
        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("category_id", sql);
    }

    [Fact]
    public void ToSql_GroupByWithWhere_IncludesWhereClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Contains("WHERE", sql);
        Assert.Contains("GROUP BY", sql);
    }

    [Fact]
    public void ToSql_GroupByWithHaving_IncludesHavingClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > 5)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("HAVING", sql);
        Assert.Contains("COUNT(*)", sql);
    }

    [Fact]
    public void ToSql_GroupByCompositeKey_IncludesMultipleColumns()
    {
        var sql = _fixture.Connection.From<Product>()
            .GroupBy(p => new { p.CategoryId, p.SupplierId })
            .ToSql(g => new { g.Key.CategoryId, g.Key.SupplierId, Count = g.Count() });

        Assert.Contains("category_id", sql);
        Assert.Contains("supplier_id", sql);
        Assert.Contains("GROUP BY", sql);
    }

    // --- Async Tests ---

    [Fact]
    public async Task SelectAsync_GroupBySingleKey_ReturnsGroupedResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.True(r.Count > 0));
    }

    [Fact]
    public async Task SelectAsync_GroupByWithWhere_FiltersCorrectly()
    {
        var results = await _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .GroupBy(p => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task SelectAsync_GroupByWithHaving_FiltersGroupsCorrectly()
    {
        var results = await _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > 5)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.All(results, r => Assert.True(r.Count > 5));
    }

    [Fact]
    public async Task SelectAsync_GroupByWithMultipleAggregates_ReturnsCorrectResults()
    {
        var results = await _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .SelectAsync(g => new
            {
                CategoryId = g.Key,
                ProductCount = g.Count(),
                TotalStock = g.Sum(p => p.UnitsInStock),
                AvgPrice = g.Avg(p => p.UnitPrice),
                MinPrice = g.Min(p => p.UnitPrice),
                MaxPrice = g.Max(p => p.UnitPrice)
            });

        Assert.NotEmpty(results);
    }

    // --- Edge Cases ---

    [Fact]
    public void GroupBy_NoResults_ReturnsEmptyList()
    {
        // Filter to non-existent category
        var results = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == -999)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        Assert.Empty(results);
    }

    [Fact]
    public void GroupBy_CountWithSelector_CountsColumn()
    {
        var results = _fixture.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                TotalCount = g.Count(),
                SupplierCount = g.Count(p => p.SupplierId)
            });

        Assert.NotEmpty(results);
        // SupplierCount should be <= TotalCount (counts non-null SupplierId only)
        Assert.All(results, r => Assert.True(r.SupplierCount <= r.TotalCount));
    }
}