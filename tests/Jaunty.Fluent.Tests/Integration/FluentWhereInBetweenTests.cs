using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentWhereInBetweenTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentWhereInBetweenTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // WhereIn Tests
    // ==========================================

    [Fact]
    public void WhereIn_WithValues_FiltersResults()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _fixture.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(categoryIds.Contains(p.CategoryId)));
    }

    [Fact]
    public void WhereIn_WithIntValues_FiltersResults()
    {
        var productIds = new[] { 1, 2, 3, 4, 5 };

        var products = _fixture.Connection.From<Product>()
            .WhereIn(p => p.ProductId, productIds)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(productIds.Contains(p.ProductId)));
    }

    [Fact]
    public void WhereIn_WithEmptyCollection_ReturnsNoResults()
    {
        var emptyIds = Array.Empty<int>();

        var products = _fixture.Connection.From<Product>()
            .WhereIn(p => p.ProductId, emptyIds)
            .Select();

        Assert.Empty(products);
    }

    [Fact]
    public void WhereIn_WithSingleValue_FiltersResults()
    {
        var singleId = new[] { 1 };

        var products = _fixture.Connection.From<Product>()
            .WhereIn(p => p.ProductId, singleId)
            .Select();

        Assert.Equal(1, products.Count);
        Assert.Equal(1, products[0].ProductId);
    }

    [Fact]
    public void WhereIn_ToSql_GeneratesInClause()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var sql = _fixture.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .ToSql();

        Assert.Contains("IN", sql);
        Assert.Contains("@p_in_", sql);
    }

    // ==========================================
    // WhereNotIn Tests
    // ==========================================

    [Fact]
    public void WhereNotIn_WithValues_FiltersResults()
    {
        var excludedCategories = new short?[] { 1, 2 };

        var products = _fixture.Connection.From<Product>()
            .WhereNotIn(p => p.CategoryId, excludedCategories)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(!excludedCategories.Contains(p.CategoryId)));
    }

    [Fact]
    public void WhereNotIn_WithEmptyCollection_ReturnsAllResults()
    {
        var emptyIds = Array.Empty<int>();

        var allProducts = _fixture.Connection.From<Product>().Select();

        var products = _fixture.Connection.From<Product>()
            .WhereNotIn(p => p.ProductId, emptyIds)
            .Select();

        // NOT IN () should return all results (1=1)
        Assert.Equal(allProducts.Count, products.Count);
    }

    [Fact]
    public void WhereNotIn_ToSql_GeneratesNotInClause()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var sql = _fixture.Connection.From<Product>()
            .WhereNotIn(p => p.CategoryId, categoryIds)
            .ToSql();

        Assert.Contains("NOT IN", sql);
        Assert.Contains("@p_in_", sql);
    }

    // ==========================================
    // AndIn / OrIn Tests
    // ==========================================

    [Fact]
    public void Where_AndIn_ChainsConditions()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndIn(p => p.CategoryId, categoryIds)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.Discontinued == false &&
            categoryIds.Contains(p.CategoryId)));
    }

    [Fact]
    public void Where_AndNotIn_ChainsConditions()
    {
        var excludedCategories = new short?[] { 1, 2 };

        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndNotIn(p => p.CategoryId, excludedCategories)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.Discontinued == false &&
            !excludedCategories.Contains(p.CategoryId)));
    }

    [Fact]
    public void Where_OrIn_ChainsConditions()
    {
        var categoryIds = new short?[] { 5, 6, 7 };

        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrIn(p => p.CategoryId, categoryIds)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.CategoryId == 1 ||
            categoryIds.Contains(p.CategoryId)));
    }

    [Fact]
    public void Where_OrNotIn_ChainsConditions()
    {
        var excludedCategories = new short?[] { 1, 2, 3, 4, 5, 6, 7 };

        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrNotIn(p => p.CategoryId, excludedCategories)
            .Select();

        Assert.NotEmpty(products);
    }

    // ==========================================
    // WhereBetween Tests
    // ==========================================

    [Fact]
    public void WhereBetween_Decimal_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 30m)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.UnitPrice >= 10m && p.UnitPrice <= 30m));
    }

    [Fact]
    public void WhereBetween_Int_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereBetween(p => p.ProductId, 1, 10)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.ProductId >= 1 && p.ProductId <= 10));
    }

    [Fact]
    public void WhereBetween_Short_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereBetween(p => p.UnitsInStock, (short?)10, (short?)50)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.UnitsInStock >= 10 && p.UnitsInStock <= 50));
    }

    [Fact]
    public void WhereBetween_ToSql_GeneratesBetweenClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 30m)
            .ToSql();

        Assert.Contains("BETWEEN", sql);
        Assert.Contains("@p_between_from_", sql);
        Assert.Contains("@p_between_to_", sql);
    }

    // ==========================================
    // WhereNotBetween Tests
    // ==========================================

    [Fact]
    public void WhereNotBetween_Decimal_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereNotBetween(p => p.UnitPrice, 10m, 30m)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.UnitPrice < 10m || p.UnitPrice > 30m));
    }

    [Fact]
    public void WhereNotBetween_Int_FiltersResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereNotBetween(p => p.ProductId, 1, 5)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.ProductId < 1 || p.ProductId > 5));
    }

    [Fact]
    public void WhereNotBetween_ToSql_GeneratesNotBetweenClause()
    {
        var sql = _fixture.Connection.From<Product>()
            .WhereNotBetween(p => p.UnitPrice, 10m, 30m)
            .ToSql();

        Assert.Contains("NOT BETWEEN", sql);
        Assert.Contains("@p_between_from_", sql);
        Assert.Contains("@p_between_to_", sql);
    }

    // ==========================================
    // AndBetween / OrBetween Tests
    // ==========================================

    [Fact]
    public void Where_AndBetween_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndBetween(p => p.UnitPrice, 10m, 50m)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.Discontinued == false &&
            p.UnitPrice >= 10m && p.UnitPrice <= 50m));
    }

    [Fact]
    public void Where_AndNotBetween_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndNotBetween(p => p.UnitPrice, 100m, 500m)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.Discontinued == false &&
            (p.UnitPrice < 100m || p.UnitPrice > 500m)));
    }

    [Fact]
    public void Where_OrBetween_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrBetween(p => p.UnitPrice, 100m, 500m)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.CategoryId == 1 ||
            (p.UnitPrice >= 100m && p.UnitPrice <= 500m)));
    }

    [Fact]
    public void Where_OrNotBetween_ChainsConditions()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 1)
            .OrNotBetween(p => p.UnitPrice, 0m, 1000m)
            .Select();

        Assert.NotEmpty(products);
    }

    // ==========================================
    // Combined In/Between Tests
    // ==========================================

    [Fact]
    public void WhereIn_AndBetween_CombinesCorrectly()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _fixture.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .AndBetween(p => p.UnitPrice, 10m, 50m)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(categoryIds.Contains(p.CategoryId) &&
            p.UnitPrice >= 10m && p.UnitPrice <= 50m));
    }

    [Fact]
    public void WhereBetween_AndIn_CombinesCorrectly()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _fixture.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .AndIn(p => p.CategoryId, categoryIds)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(categoryIds.Contains(p.CategoryId) &&
            p.UnitPrice >= 10m && p.UnitPrice <= 50m));
    }

    [Fact]
    public void Where_AndIn_AndBetween_CombinesMultiple()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndIn(p => p.CategoryId, categoryIds)
            .AndBetween(p => p.UnitPrice, 5m, 100m)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.Discontinued == false &&
            categoryIds.Contains(p.CategoryId) &&
            p.UnitPrice >= 5m && p.UnitPrice <= 100m));
    }

    // ==========================================
    // With OrderBy Tests
    // ==========================================

    [Fact]
    public void WhereIn_OrderBy_SortsFilteredResults()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = _fixture.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .OrderBy(p => p.UnitPrice)
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True((products[i - 1].UnitPrice ?? 0) <= (products[i].UnitPrice ?? 0));
        }
    }

    [Fact]
    public void WhereBetween_OrderByDescending_SortsFilteredResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .OrderByDescending(p => p.UnitPrice)
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True((products[i - 1].UnitPrice ?? 0) >= (products[i].UnitPrice ?? 0));
        }
    }

    // ==========================================
    // With Take/Skip Tests
    // ==========================================

    [Fact]
    public void WhereIn_Take_LimitsResults()
    {
        var categoryIds = new short?[] { 1, 2, 3, 4, 5 };

        var products = _fixture.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .Take(5)
            .Select();

        Assert.True(products.Count <= 5);
    }

    [Fact]
    public void WhereBetween_Skip_Take_PaginatesResults()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereBetween(p => p.ProductId, 1, 50)
            .OrderBy(p => p.ProductId)
            .Skip(5)
            .Take(5)
            .Select();

        Assert.True(products.Count <= 5);
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task WhereIn_SelectAsync_FiltersCorrectly()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var products = await _fixture.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .SelectAsync();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(categoryIds.Contains(p.CategoryId)));
    }

    [Fact]
    public async Task WhereBetween_SelectAsync_FiltersCorrectly()
    {
        var products = await _fixture.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .SelectAsync();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.UnitPrice >= 10m && p.UnitPrice <= 50m));
    }

    [Fact]
    public async Task WhereIn_CountAsync_ReturnsFilteredCount()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var count = await _fixture.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .CountAsync();

        Assert.True(count > 0);
    }

    [Fact]
    public async Task WhereBetween_CountAsync_ReturnsFilteredCount()
    {
        var count = await _fixture.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .CountAsync();

        Assert.True(count > 0);
    }

    // ==========================================
    // With GroupBy Tests
    // ==========================================

    [Fact]
    public void WhereIn_GroupBy_GroupsFilteredResults()
    {
        var categoryIds = new short?[] { 1, 2, 3 };

        var grouped = _fixture.Connection.From<Product>()
            .WhereIn(p => p.CategoryId, categoryIds)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { Category = g.Key, Count = g.Count() });

        Assert.NotEmpty(grouped);
        Assert.All(grouped, g => Assert.True(categoryIds.Contains(g.Category)));
    }

    [Fact]
    public void WhereBetween_GroupBy_GroupsFilteredResults()
    {
        var grouped = _fixture.Connection.From<Product>()
            .WhereBetween(p => p.UnitPrice, 10m, 50m)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { Category = g.Key, AvgPrice = g.Avg(p => p.UnitPrice) });

        Assert.NotEmpty(grouped);
    }
}