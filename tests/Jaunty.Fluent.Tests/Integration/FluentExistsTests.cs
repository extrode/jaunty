using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentExistsTests : IDisposable
{
    private readonly Database _db;

    public FluentExistsTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // WhereExists Tests
    // ==========================================

    [Fact]
    public void WhereExists_FindsCategoriesWithProducts()
    {
        // Find categories that have at least one product
        var categories = _db.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        categories.Should().NotBeEmpty();

        // All returned categories should have products
        foreach (var category in categories)
        {
            var productCount = _db.Connection.From<Product>()
                .Where(p => p.CategoryId == category.CategoryId)
                .Count();
            productCount.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void WhereExists_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        sql.Should().Contain("EXISTS");
        sql.Should().Contain("SELECT 1 FROM");
        sql.Should().Contain("products");
        sql.Should().Contain("WHERE");
        sql.Should().Contain("category_id");
    }

    // ==========================================
    // WhereNotExists Tests
    // ==========================================

    [Fact]
    public void WhereNotExists_FindsCategoriesWithoutProducts()
    {
        // Find categories that have no products
        var categoriesWithNoProducts = _db.Connection.From<Category>()
            .WhereNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        // In Northwind, all categories should have products, so this might be empty
        // But the SQL should be valid and execute without errors
        foreach (var category in categoriesWithNoProducts)
        {
            var productCount = _db.Connection.From<Product>()
                .Where(p => p.CategoryId == category.CategoryId)
                .Count();
            productCount.Should().Be(0);
        }
    }

    [Fact]
    public void WhereNotExists_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Category>()
            .WhereNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        sql.Should().Contain("NOT EXISTS");
        sql.Should().Contain("SELECT 1 FROM");
        sql.Should().Contain("products");
    }

    // ==========================================
    // AndExists / AndNotExists Tests
    // ==========================================

    [Fact]
    public void AndExists_CombinesWithWhere()
    {
        // Categories that start with 'C' and have products
        var categories = _db.Connection.From<Category>()
            .Where(c => c.CategoryName!.StartsWith("C"))
            .AndExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        categories.Should().NotBeEmpty();
        categories.Should().OnlyContain(c => c.CategoryName!.StartsWith("C"));
    }

    [Fact]
    public void AndNotExists_CombinesWithWhere()
    {
        var sql = _db.Connection.From<Category>()
            .Where(c => c.CategoryId > 0)
            .AndNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        sql.Should().Contain("WHERE");
        sql.Should().Contain("AND NOT EXISTS");
    }

    // ==========================================
    // OrExists / OrNotExists Tests
    // ==========================================

    [Fact]
    public void OrExists_CombinesWithWhere()
    {
        // Categories named "Beverages" or categories that have products with high stock
        var sql = _db.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages")
            .OrExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.UnitsInStock > 100)
            .ToSql();

        sql.Should().Contain("OR EXISTS");
        sql.Should().Contain("units_in_stock");
    }

    [Fact]
    public void OrNotExists_ToSql_GeneratesCorrectSql()
    {
        var sql = _db.Connection.From<Category>()
            .Where(c => c.CategoryId == 1)
            .OrNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        sql.Should().Contain("OR NOT EXISTS");
    }

    // ==========================================
    // Complex Predicate Tests
    // ==========================================

    [Fact]
    public void WhereExists_WithComplexPredicate_FiltersCorrectly()
    {
        // Find categories that have discontinued products
        var categoriesWithDiscontinued = _db.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.Discontinued == true)
            .Select();

        // Verify each returned category has at least one discontinued product
        foreach (var category in categoriesWithDiscontinued)
        {
            var discontinuedCount = _db.Connection.From<Product>()
                .Where(p => p.CategoryId == category.CategoryId)
                .And(p => p.Discontinued == true)
                .Count();
            discontinuedCount.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void WhereExists_WithMultipleConditions_ToSql()
    {
        var sql = _db.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.UnitPrice > 50 && p.Discontinued == false)
            .ToSql();

        sql.Should().Contain("EXISTS");
        sql.Should().Contain("category_id");
        sql.Should().Contain("unit_price");
        sql.Should().Contain("discontinued");
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task WhereExists_SelectAsync_ReturnsCorrectResults()
    {
        var categories = await _db.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .SelectAsync();

        categories.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WhereNotExists_CountAsync_ReturnsValidCount()
    {
        var count = await _db.Connection.From<Category>()
            .WhereNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .CountAsync();

        // Count should be valid (>= 0)
        count.Should().BeGreaterThanOrEqualTo(0);
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void WhereExists_WithOrderBy_WorksCorrectly()
    {
        var categories = _db.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .OrderBy(c => c.CategoryName)
            .Select();

        categories.Should().NotBeEmpty();
        categories.Should().BeInAscendingOrder(c => c.CategoryName);
    }

    [Fact]
    public void WhereExists_WithTake_LimitsResults()
    {
        var categories = _db.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Take(3)
            .Select();

        categories.Should().HaveCountLessThanOrEqualTo(3);
    }
}
