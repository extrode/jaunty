using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentGroupByTests : IDisposable
{
    private readonly Database _db;

    public FluentGroupByTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // --- Basic GROUP BY Tests ---

    [Fact]
    public void GroupBy_SingleKey_WithCount_ReturnsGroupedResults()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        results.Should().NotBeEmpty();
        results.All(r => r.Count > 0).Should().BeTrue();
    }

    [Fact]
    public void GroupBy_SingleKey_WithSum_ReturnsGroupedResults()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, TotalStock = g.Sum(p => p.UnitsInStock) });

        results.Should().NotBeEmpty();
    }

    [Fact]
    public void GroupBy_SingleKey_WithAvg_ReturnsGroupedResults()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, AvgStock = g.Avg(p => p.UnitsInStock) });

        results.Should().NotBeEmpty();
    }

    [Fact]
    public void GroupBy_SingleKey_WithMin_ReturnsGroupedResults()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, MinStock = g.Min(p => p.UnitsInStock) });

        results.Should().NotBeEmpty();
    }

    [Fact]
    public void GroupBy_SingleKey_WithMax_ReturnsGroupedResults()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, MaxStock = g.Max(p => p.UnitsInStock) });

        results.Should().NotBeEmpty();
    }

    [Fact]
    public void GroupBy_SingleKey_MultipleAggregates_ReturnsGroupedResults()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                ProductCount = g.Count(),
                TotalStock = g.Sum(p => p.UnitsInStock),
                AvgStock = g.Avg(p => p.UnitsInStock)
            });

        results.Should().NotBeEmpty();
        results.All(r => r.ProductCount > 0).Should().BeTrue();
    }

    // --- GROUP BY with WHERE Tests ---

    [Fact]
    public void GroupBy_WithWhere_FiltersBeforeGrouping()
    {
        // Group only non-discontinued products
        var results = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        results.Should().NotBeEmpty();

        // Verify count is less than total without filter
        var totalCount = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .Count();

        results.Sum(r => r.Count).Should().Be(totalCount);
    }

    [Fact]
    public void GroupBy_WithWhereOnCategoryId_FiltersCorrectly()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Count = g.Count() });

        results.Should().NotBeEmpty();
    }

    // --- GROUP BY with HAVING Tests ---

    [Fact]
    public void GroupBy_WithHaving_FiltersGroupsAfterGrouping()
    {
        // Only return groups with more than 5 products
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > 5)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        results.All(r => r.Count > 5).Should().BeTrue();
    }

    [Fact]
    public void GroupBy_WithHaving_CountGreaterThanZero_ReturnsNonEmptyGroups()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > 0)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        results.Should().NotBeEmpty();
        results.All(r => r.Count > 0).Should().BeTrue();
    }

    // --- GROUP BY with Composite Key Tests ---

    [Fact]
    public void GroupBy_CompositeKey_ReturnsGroupedResults()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => new { p.CategoryId, p.SupplierId })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.SupplierId,
                Count = g.Count()
            });

        results.Should().NotBeEmpty();
    }

    [Fact]
    public void GroupBy_CompositeKey_WithAggregates_ReturnsGroupedResults()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => new { p.CategoryId, p.SupplierId })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.SupplierId,
                ProductCount = g.Count(),
                TotalStock = g.Sum(p => p.UnitsInStock)
            });

        results.Should().NotBeEmpty();
    }

    // --- ToSql Tests ---

    [Fact]
    public void ToSql_SimpleGroupBy_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        sql.Should().Contain("SELECT");
        sql.Should().Contain("COUNT(*)");
        sql.Should().Contain("GROUP BY");
        sql.Should().Contain("category_id");
    }

    [Fact]
    public void ToSql_GroupByWithWhere_IncludesWhereClause()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .GroupBy(p => p.CategoryId)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        sql.Should().Contain("WHERE");
        sql.Should().Contain("GROUP BY");
    }

    [Fact]
    public void ToSql_GroupByWithHaving_IncludesHavingClause()
    {
        var sql = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > 5)
            .ToSql(g => new { CategoryId = g.Key, Count = g.Count() });

        sql.Should().Contain("GROUP BY");
        sql.Should().Contain("HAVING");
        sql.Should().Contain("COUNT(*)");
    }

    [Fact]
    public void ToSql_GroupByCompositeKey_IncludesMultipleColumns()
    {
        var sql = _db.Connection.From<Product>()
            .GroupBy(p => new { p.CategoryId, p.SupplierId })
            .ToSql(g => new { g.Key.CategoryId, g.Key.SupplierId, Count = g.Count() });

        sql.Should().Contain("category_id");
        sql.Should().Contain("supplier_id");
        sql.Should().Contain("GROUP BY");
    }

    // --- Async Tests ---

    [Fact]
    public async Task SelectAsync_GroupBySingleKey_ReturnsGroupedResults()
    {
        var results = await _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        results.Should().NotBeEmpty();
        results.All(r => r.Count > 0).Should().BeTrue();
    }

    [Fact]
    public async Task SelectAsync_GroupByWithWhere_FiltersCorrectly()
    {
        var results = await _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .GroupBy(p => p.CategoryId)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SelectAsync_GroupByWithHaving_FiltersGroupsCorrectly()
    {
        var results = await _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Having(g => g.Count() > 5)
            .SelectAsync(g => new { CategoryId = g.Key, Count = g.Count() });

        results.All(r => r.Count > 5).Should().BeTrue();
    }

    [Fact]
    public async Task SelectAsync_GroupByWithMultipleAggregates_ReturnsCorrectResults()
    {
        var results = await _db.Connection.From<Product>()
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

        results.Should().NotBeEmpty();
    }

    // --- Edge Cases ---

    [Fact]
    public void GroupBy_NoResults_ReturnsEmptyList()
    {
        // Filter to non-existent category
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == -999)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() });

        results.Should().BeEmpty();
    }

    [Fact]
    public void GroupBy_CountWithSelector_CountsColumn()
    {
        var results = _db.Connection.From<Product>()
            .GroupBy(p => p.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                TotalCount = g.Count(),
                SupplierCount = g.Count(p => p.SupplierId)
            });

        results.Should().NotBeEmpty();
        // SupplierCount should be <= TotalCount (counts non-null SupplierId only)
        results.All(r => r.SupplierCount <= r.TotalCount).Should().BeTrue();
    }
}
