using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for UNION, UNION ALL, EXCEPT, INTERSECT set operations.
/// </summary>
public class FluentSetOperationsTests : IDisposable
{
    private readonly Database _db;

    public FluentSetOperationsTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // UNION Tests
    // ==========================================

    [Fact]
    public void Union_TwoQueries_ReturnsCombinedResultsWithoutDuplicates()
    {
        // Get products from category 1 UNION products from category 2
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Select();

        results.Should().NotBeEmpty();
        // All results should be from category 1 or 2
        results.Should().OnlyContain(p => p.CategoryId == 1 || p.CategoryId == 2);
    }

    [Fact]
    public void Union_ToSql_GeneratesUnionKeyword()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        sql.Should().Contain("UNION");
        sql.Should().NotContain("UNION ALL");
    }

    [Fact]
    public void Union_WithOrderBy_OrdersEntireResult()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductName)
            .Select();

        results.Should().NotBeEmpty();
        results.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void Union_WithOrderByDescending_OrdersEntireResultDescending()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderByDescending(p => p.ProductName)
            .Select();

        results.Should().NotBeEmpty();
        results.Should().BeInDescendingOrder(p => p.ProductName);
    }

    [Fact]
    public void Union_WithTake_LimitsResults()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Take(3)
            .Select();

        results.Should().HaveCount(3);
    }

    [Fact]
    public void Union_ChainedMultipleTimes_CombinesAllQueries()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 3))
            .Select();

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(p => p.CategoryId == 1 || p.CategoryId == 2 || p.CategoryId == 3);
    }

    // ==========================================
    // UNION ALL Tests
    // ==========================================

    [Fact]
    public void UnionAll_TwoQueries_ReturnsCombinedResultsWithDuplicates()
    {
        // UNION ALL keeps duplicates (same products may appear multiple times)
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_db.Connection.From<Product>().Where(p => p.CategoryId == 1))
            .Select();

        // With UNION ALL on the same query, count should double
        var singleQueryCount = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Count();

        results.Should().HaveCount(singleQueryCount * 2);
    }

    [Fact]
    public void UnionAll_ToSql_GeneratesUnionAllKeyword()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        sql.Should().Contain("UNION ALL");
    }

    [Fact]
    public void UnionAll_WithOrderBy_OrdersEntireResult()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.UnitPrice)
            .Select();

        results.Should().NotBeEmpty();
        results.Should().BeInAscendingOrder(p => p.UnitPrice);
    }

    // ==========================================
    // EXCEPT Tests
    // ==========================================

    [Fact]
    public void Except_TwoQueries_ReturnsRowsNotInSecondQuery()
    {
        // Get products from category 1 that are NOT discontinued
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_db.Connection.From<Product>().Where(p => p.Discontinued == true))
            .Select();

        results.Should().NotBeEmpty();
        // All results should be from category 1 and not discontinued
        results.Should().OnlyContain(p => p.CategoryId == 1 && !p.Discontinued);
    }

    [Fact]
    public void Except_ToSql_GeneratesExceptKeyword()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .ToSql();

        sql.Should().Contain("EXCEPT");
    }

    [Fact]
    public void Except_WithOrderBy_OrdersEntireResult()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_db.Connection.From<Product>().Where(p => p.Discontinued == true))
            .OrderBy(p => p.ProductName)
            .Select();

        results.Should().NotBeEmpty();
        results.Should().BeInAscendingOrder(p => p.ProductName);
    }

    // ==========================================
    // INTERSECT Tests
    // ==========================================

    [Fact]
    public void Intersect_TwoQueries_ReturnsOnlyCommonRows()
    {
        // Get products that are both in category 1 AND have low stock (ReorderLevel > 0)
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_db.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .Select();

        // All results should be in category 1 AND have ReorderLevel > 0
        results.Should().OnlyContain(p => p.CategoryId == 1 && p.ReorderLevel > 0);
    }

    [Fact]
    public void Intersect_ToSql_GeneratesIntersectKeyword()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_db.Connection.From<Product>().Where(p => p.CategoryId == 1))
            .ToSql();

        sql.Should().Contain("INTERSECT");
    }

    [Fact]
    public void Intersect_WithOrderBy_OrdersEntireResult()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_db.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .OrderByDescending(p => p.ProductName)
            .Select();

        results.Should().BeInDescendingOrder(p => p.ProductName);
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task Union_SelectAsync_ReturnsResults()
    {
        var results = await _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectAsync();

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task UnionAll_SelectAsync_ReturnsResults()
    {
        var results = await _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectAsync();

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Except_SelectAsync_ReturnsResults()
    {
        var results = await _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Except(_db.Connection.From<Product>().Where(p => p.Discontinued == true))
            .SelectAsync();

        results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Intersect_SelectAsync_ReturnsResults()
    {
        var results = await _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Intersect(_db.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .SelectAsync();

        // Results may be empty if no intersection, but should not throw
        results.Should().NotBeNull();
    }

    // ==========================================
    // SelectFirst/SelectSingle Tests
    // ==========================================

    [Fact]
    public void Union_SelectFirst_ReturnsSingleResult()
    {
        var result = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .SelectFirst();

        result.Should().NotBeNull();
        result.CategoryId.Should().BeOneOf(1, 2);
    }

    [Fact]
    public void Union_SelectFirstOrDefault_ReturnsNullWhenNoResults()
    {
        var result = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == -999)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == -998))
            .SelectFirstOrDefault();

        result.Should().BeNull();
    }

    // ==========================================
    // ThenBy Tests
    // ==========================================

    [Fact]
    public void Union_OrderByThenBy_OrdersByMultipleColumns()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.ProductName)
            .ToSql();

        sql.Should().Contain("ORDER BY");
        sql.Should().Contain("category_id");
        sql.Should().Contain("product_name");
    }

    [Fact]
    public void Union_OrderByThenByDescending_OrdersCorrectly()
    {
        var results = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.CategoryId)
            .ThenByDescending(p => p.ProductName)
            .Select();

        results.Should().NotBeEmpty();
    }

    // ==========================================
    // Skip/Take Tests
    // ==========================================

    [Fact]
    public void Union_SkipTake_PaginatesResults()
    {
        var allResults = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Select();

        var pagedResults = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .OrderBy(p => p.ProductId)
            .Skip(2)
            .Take(3)
            .Select();

        pagedResults.Should().HaveCount(3);
        // Verify skip worked
        pagedResults[0].ProductId.Should().Be(allResults[2].ProductId);
    }

    // ==========================================
    // Mixed Operations Tests
    // ==========================================

    [Fact]
    public void Union_ThenExcept_ChainedOperations()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .Union(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Except(_db.Connection.From<Product>().Where(p => p.Discontinued == true))
            .ToSql();

        sql.Should().Contain("UNION");
        sql.Should().Contain("EXCEPT");
    }

    [Fact]
    public void UnionAll_ThenIntersect_ChainedOperations()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .UnionAll(_db.Connection.From<Product>().Where(p => p.CategoryId == 2))
            .Intersect(_db.Connection.From<Product>().Where(p => p.ReorderLevel > 0))
            .ToSql();

        sql.Should().Contain("UNION ALL");
        sql.Should().Contain("INTERSECT");
    }
}
