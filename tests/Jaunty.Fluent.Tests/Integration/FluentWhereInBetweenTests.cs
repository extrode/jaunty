using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentWhereInBetweenTests : IDisposable
{
    private readonly Database _db;

    public FluentWhereInBetweenTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // WhereIn Tests
    // ==========================================

    [Fact]
    public void WhereIn_WithValues_FiltersResults()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _db.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => categoryIds.Contains(p.CategoryId));
    }

    [Fact]
    public void WhereIn_WithIntValues_FiltersResults()
    {
        var productIds = new[] { 1, 2, 3, 4, 5 };

        var products = _db.Connection.From<Product>()
            .WhereIn(p => p.ProductId, productIds)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => productIds.Contains(p.ProductId));
    }

    [Fact]
    public void WhereIn_WithEmptyCollection_ReturnsNoResults()
    {
        var emptyIds = Array.Empty<int>();

        var products = _db.Connection.From<Product>()
            .WhereIn(p => p.ProductId, emptyIds)
            .Select();

        products.Should().BeEmpty();
    }

    [Fact]
    public void WhereIn_WithSingleValue_FiltersResults()
    {
        var singleId = new[] { 1 };

        var products = _db.Connection.From<Product>()
            .WhereIn(p => p.ProductId, singleId)
            .Select();

        products.Should().HaveCount(1);
        products[0].ProductId.Should().Be(1);
    }

    [Fact]
    public void WhereIn_ToSql_GeneratesInClause()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var sql = _db.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .ToSql();

        sql.Should().Contain("IN");
        sql.Should().Contain("@p_in_");
    }

    // ==========================================
    // WhereNotIn Tests
    // ==========================================

    [Fact]
    public void WhereNotIn_WithValues_FiltersResults()
    {
        var excludedCategories = new short?[] { 1, 2 };

        var products = _db.Connection.From<Product>()
            .WhereNotIn(p => p.CategoryId, excludedCategories)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => !excludedCategories.Contains(p.CategoryId));
    }

    [Fact]
    public void WhereNotIn_WithEmptyCollection_ReturnsAllResults()
    {
        var emptyIds = Array.Empty<int>();

        var allProducts = _db.Connection.From<Product>().Select();

        var products = _db.Connection.From<Product>()
            .WhereNotIn(p => p.ProductId, emptyIds)
            .Select();

        // NOT IN () should return all results (1=1)
        products.Count.Should().Be(allProducts.Count);
    }

    [Fact]
    public void WhereNotIn_ToSql_GeneratesNotInClause()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var sql = _db.Connection.From<Product>()
            .WhereNotIn(p => p.CategoryId, categoryIds)
            .ToSql();

        sql.Should().Contain("NOT IN");
        sql.Should().Contain("@p_in_");
    }

    // ==========================================
    // AndIn / OrIn Tests
    // ==========================================

    [Fact]
    public void Where_AndIn_ChainsConditions()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndIn(p => p.CategoryId, categoryIds)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.Discontinued == false &&
            categoryIds.Contains(p.CategoryId));
    }

    [Fact]
    public void Where_AndNotIn_ChainsConditions()
    {
        var excludedCategories = new short?[] { 1, 2 };

        var products = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndNotIn(p => p.CategoryId, excludedCategories)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.Discontinued == false &&
            !excludedCategories.Contains(p.CategoryId));
    }

    [Fact]
    public void Where_OrIn_ChainsConditions()
    {
        var categoryIds = new short?[] { 5, 6, 7 };

        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrIn(p => p.CategoryId, categoryIds)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.CategoryId == 1 ||
            categoryIds.Contains(p.CategoryId));
    }

    [Fact]
    public void Where_OrNotIn_ChainsConditions()
    {
        var excludedCategories = new short?[] { 1, 2, 3, 4, 5, 6, 7 };

        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrNotIn(p => p.CategoryId, excludedCategories)
            .Select();

        products.Should().NotBeEmpty();
    }

    // ==========================================
    // WhereBetween Tests
    // ==========================================

    [Fact]
    public void WhereBetween_Decimal_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 30m)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.UnitPrice >= 10m && p.UnitPrice <= 30m);
    }

    [Fact]
    public void WhereBetween_Int_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .WhereBetween(p => p.ProductId, 1, 10)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.ProductId >= 1 && p.ProductId <= 10);
    }

    [Fact]
    public void WhereBetween_Short_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .WhereBetween(p => p.UnitsInStock, (short?)10, (short?)50)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.UnitsInStock >= 10 && p.UnitsInStock <= 50);
    }

    [Fact]
    public void WhereBetween_ToSql_GeneratesBetweenClause()
    {
        var sql = _db.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 30m)
            .ToSql();

        sql.Should().Contain("BETWEEN");
        sql.Should().Contain("@p_between_from_");
        sql.Should().Contain("@p_between_to_");
    }

    // ==========================================
    // WhereNotBetween Tests
    // ==========================================

    [Fact]
    public void WhereNotBetween_Decimal_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .WhereNotBetween(p => p.UnitPrice, 10m, 30m)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.UnitPrice < 10m || p.UnitPrice > 30m);
    }

    [Fact]
    public void WhereNotBetween_Int_FiltersResults()
    {
        var products = _db.Connection.From<Product>()
            .WhereNotBetween(p => p.ProductId, 1, 5)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.ProductId < 1 || p.ProductId > 5);
    }

    [Fact]
    public void WhereNotBetween_ToSql_GeneratesNotBetweenClause()
    {
        var sql = _db.Connection.From<Product>()
            .WhereNotBetween(p => p.UnitPrice, 10m, 30m)
            .ToSql();

        sql.Should().Contain("NOT BETWEEN");
        sql.Should().Contain("@p_between_from_");
        sql.Should().Contain("@p_between_to_");
    }

    // ==========================================
    // AndBetween / OrBetween Tests
    // ==========================================

    [Fact]
    public void Where_AndBetween_ChainsConditions()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndBetween(p => p.UnitPrice, 10m, 50m)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.Discontinued == false &&
            p.UnitPrice >= 10m && p.UnitPrice <= 50m);
    }

    [Fact]
    public void Where_AndNotBetween_ChainsConditions()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndNotBetween(p => p.UnitPrice, 100m, 500m)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.Discontinued == false &&
            (p.UnitPrice < 100m || p.UnitPrice > 500m));
    }

    [Fact]
    public void Where_OrBetween_ChainsConditions()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrBetween(p => p.UnitPrice, 100m, 500m)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.CategoryId == 1 ||
            (p.UnitPrice >= 100m && p.UnitPrice <= 500m));
    }

    [Fact]
    public void Where_OrNotBetween_ChainsConditions()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrNotBetween(p => p.UnitPrice, 0m, 1000m)
            .Select();

        products.Should().NotBeEmpty();
    }

    // ==========================================
    // Combined In/Between Tests
    // ==========================================

    [Fact]
    public void WhereIn_AndBetween_CombinesCorrectly()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _db.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .AndBetween(p => p.UnitPrice, 10m, 50m)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            categoryIds.Contains(p.CategoryId) &&
            p.UnitPrice >= 10m && p.UnitPrice <= 50m);
    }

    [Fact]
    public void WhereBetween_AndIn_CombinesCorrectly()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _db.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .AndIn(p => p.CategoryId, categoryIds)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            categoryIds.Contains(p.CategoryId) &&
            p.UnitPrice >= 10m && p.UnitPrice <= 50m);
    }

    [Fact]
    public void Where_AndIn_AndBetween_CombinesMultiple()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndIn(p => p.CategoryId, categoryIds)
            .AndBetween(p => p.UnitPrice, 5m, 100m)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.Discontinued == false &&
            categoryIds.Contains(p.CategoryId) &&
            p.UnitPrice >= 5m && p.UnitPrice <= 100m);
    }

    // ==========================================
    // With OrderBy Tests
    // ==========================================

    [Fact]
    public void WhereIn_OrderBy_SortsFilteredResults()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _db.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .OrderBy(p => p.UnitPrice)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.UnitPrice);
    }

    [Fact]
    public void WhereBetween_OrderByDescending_SortsFilteredResults()
    {
        var products = _db.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .OrderByDescending(p => p.UnitPrice)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInDescendingOrder(p => p.UnitPrice);
    }

    // ==========================================
    // With Take/Skip Tests
    // ==========================================

    [Fact]
    public void WhereIn_Take_LimitsResults()
    {
        var categoryIds = new short?[] { 1, 2, 3, 4, 5 };

        var products = _db.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .Take(5)
            .Select();

        products.Should().HaveCountLessThanOrEqualTo(5);
    }

    [Fact]
    public void WhereBetween_Skip_Take_PaginatesResults()
    {
        var products = _db.Connection.From<Product>()
            .WhereBetween(p => p.ProductId, 1, 50)
            .OrderBy(p => p.ProductId)
            .Skip(5)
            .Take(5)
            .Select();

        products.Should().HaveCountLessThanOrEqualTo(5);
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task WhereIn_SelectAsync_FiltersCorrectly()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = await _db.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .SelectAsync();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => categoryIds.Contains(p.CategoryId));
    }

    [Fact]
    public async Task WhereBetween_SelectAsync_FiltersCorrectly()
    {
        var products = await _db.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .SelectAsync();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.UnitPrice >= 10m && p.UnitPrice <= 50m);
    }

    [Fact]
    public async Task WhereIn_CountAsync_ReturnsFilteredCount()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var count = await _db.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .CountAsync();

        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WhereBetween_CountAsync_ReturnsFilteredCount()
    {
        var count = await _db.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .CountAsync();

        count.Should().BeGreaterThan(0);
    }

    // ==========================================
    // With GroupBy Tests
    // ==========================================

    [Fact]
    public void WhereIn_GroupBy_GroupsFilteredResults()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var grouped = _db.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { Category = g.Key, Count = g.Count() });

        grouped.Should().NotBeEmpty();
        grouped.Should().OnlyContain(g => categoryIds.Contains(g.Category));
    }

    [Fact]
    public void WhereBetween_GroupBy_GroupsFilteredResults()
    {
        var grouped = _db.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { Category = g.Key, AvgPrice = g.Avg(p => p.UnitPrice) });

        grouped.Should().NotBeEmpty();
    }
}
