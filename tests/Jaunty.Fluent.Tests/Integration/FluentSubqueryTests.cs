using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for subquery support (IN subquery, NOT IN subquery).
/// </summary>
public class FluentSubqueryTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentSubqueryTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // SQL Generation Tests
    // ==========================================

    [Fact]
    public void WhereInSubquery_ToSql_GeneratesCorrectSyntax()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages");

        var sql = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .ToSql();

        Assert.Contains("IN (SELECT", sql);
        Assert.Contains("category_id", sql);
        Assert.Contains("categories", sql);
    }

    [Fact]
    public void WhereNotInSubquery_ToSql_GeneratesCorrectSyntax()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages");

        var sql = _fixture.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .ToSql();

        Assert.Contains("NOT IN (SELECT", sql);
        Assert.Contains("category_id", sql);
    }

    [Fact]
    public void AndInSubquery_ToSql_GeneratesCorrectSyntax()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages");

        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .ToSql();

        Assert.Contains("discontinued", sql);
        Assert.Contains("AND", sql);
        Assert.Contains("IN (SELECT", sql);
    }

    [Fact]
    public void OrInSubquery_ToSql_GeneratesCorrectSyntax()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages");

        var sql = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == 99)
            .OrInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .ToSql();

        Assert.Contains("category_id = @", sql);
        Assert.Contains("OR", sql);
        Assert.Contains("IN (SELECT", sql);
    }

    // ==========================================
    // Query Execution Tests - WhereInSubquery
    // ==========================================

    [Fact]
    public void WhereInSubquery_FiltersByCategory_ReturnsCorrectProducts()
    {
        // Get categories that start with 'B' (Beverages, etc.)
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("B"));

        var products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        Assert.NotEmpty(products);

        // Verify all products are in categories starting with 'B'
        var validCategoryIds = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("B"))
            .Select()
            .Select(c => c.CategoryId)
            .ToHashSet();

        Assert.All(products, p => Assert.True(validCategoryIds.Contains(p.CategoryId!.Value)));
    }

    [Fact]
    public void WhereInSubquery_WithMultipleConditions_ReturnsCorrectProducts()
    {
        // Subquery: categories that have description
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.Description != null);

        var products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        Assert.NotEmpty(products);
    }

    // ==========================================
    // Query Execution Tests - WhereNotInSubquery
    // ==========================================

    [Fact]
    public void WhereNotInSubquery_ExcludesMatchingProducts()
    {
        // Get the first category
        var firstCategory = _fixture.Connection.From<Category>()
            .Take(1)
            .SelectFirst();

        // Subquery: select that category's ID
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryId == firstCategory.CategoryId);

        var products = _fixture.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId != firstCategory.CategoryId));
    }

    [Fact]
    public void WhereNotInSubquery_WithNoMatchingSubquery_ReturnsAllProducts()
    {
        // Subquery: categories with impossible name
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName == "NonExistent12345");

        var productsWithNotIn = _fixture.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        var allProducts = _fixture.Connection.From<Product>().Select();

        Assert.Equal(allProducts.Count, productsWithNotIn.Count);
    }

    // ==========================================
    // Query Execution Tests - AndInSubquery / OrInSubquery
    // ==========================================

    [Fact]
    public void AndInSubquery_CombinesWithPreviousCondition()
    {
        // Subquery: categories starting with 'C'
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        // Get valid category IDs
        var validCategoryIds = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"))
            .Select()
            .Select(c => c.CategoryId)
            .ToHashSet();

        Assert.All(products, p =>
            Assert.True(p.Discontinued == false && validCategoryIds.Contains(p.CategoryId!.Value)));
    }

    [Fact]
    public void AndNotInSubquery_CombinesWithPreviousCondition()
    {
        // Get first category
        var firstCategory = _fixture.Connection.From<Category>()
            .Take(1)
            .SelectFirst();

        // Subquery: the first category
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryId == firstCategory.CategoryId);

        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        Assert.All(products, p =>
            Assert.True(p.Discontinued == false && p.CategoryId != firstCategory.CategoryId));
    }

    [Fact]
    public void OrInSubquery_ProvidersAlternativeMatch()
    {
        // Get first two categories
        var categories = _fixture.Connection.From<Category>()
            .Take(2)
            .Select();

        var cat1Id = categories[0].CategoryId;
        var cat2Id = categories.Count > 1 ? categories[1].CategoryId : cat1Id;

        // Subquery: second category
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryId == cat2Id);

        var products = _fixture.Connection.From<Product>()
            .Where(p => p.CategoryId == cat1Id)
            .OrInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        Assert.All(products, p =>
            Assert.True(p.CategoryId == cat1Id || p.CategoryId == cat2Id));
    }

    [Fact]
    public void OrNotInSubquery_ProvidersAlternativeMatch()
    {
        // Get first category
        var firstCategory = _fixture.Connection.From<Category>()
            .Take(1)
            .SelectFirst();

        // Subquery: the first category
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryId == firstCategory.CategoryId);

        // Products that are discontinued OR not in first category
        var products = _fixture.Connection.From<Product>()
            .Where(p => p.Discontinued == true)
            .OrNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        Assert.All(products, p =>
            Assert.True(p.Discontinued == true || p.CategoryId != firstCategory.CategoryId));
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task WhereInSubquery_SelectAsync_ReturnsCorrectProducts()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = await _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .SelectAsync();

        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task WhereInSubquery_CountAsync_ReturnsCorrectCount()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var count = await _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .CountAsync();

        Assert.True(count > 0);
    }

    // ==========================================
    // Combined with Other Features
    // ==========================================

    [Fact]
    public void WhereInSubquery_WithOrderBy_WorksCorrectly()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .OrderBy(p => p.ProductName)
            .Select();

        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void WhereInSubquery_WithTake_LimitsResults()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Take(3)
            .Select();

        Assert.True(products.Count <= 3);
    }

    [Fact]
    public void WhereInSubquery_WithDistinct_ReturnsUniqueResults()
    {
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        // All product IDs should be unique
        var productIds = products.Select(p => p.ProductId).ToList();
        Assert.Equal(productIds.Distinct().Count(), productIds.Count);
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void WhereInSubquery_SubqueryReturnsNoRows_ReturnsNoResults()
    {
        // Subquery that returns no results
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName == "NonExistentCategory12345");

        var products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        Assert.Empty(products);
    }

    [Fact]
    public void WhereNotInSubquery_SubqueryReturnsNoRows_ReturnsAllResults()
    {
        // Subquery that returns no results
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName == "NonExistentCategory12345");

        var products = _fixture.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        var allProducts = _fixture.Connection.From<Product>().Select();
        Assert.Equal(allProducts.Count, products.Count);
    }

    [Fact]
    public void WhereInSubquery_ChainedSubqueries_WorksCorrectly()
    {
        // First subquery
        var subquery1 = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("B"));

        // Use first subquery in main query, then add another condition
        var products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery1)
            .And(p => p.Discontinued == false)
            .Select();

        var validCategoryIds = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("B"))
            .Select()
            .Select(c => c.CategoryId)
            .ToHashSet();

        Assert.All(products, p =>
            Assert.True(validCategoryIds.Contains(p.CategoryId!.Value) && p.Discontinued == false));
    }

    [Fact]
    public void WhereInSubquery_ComplexSubquery_WorksCorrectly()
    {
        // Complex subquery with multiple conditions
        var subquery = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName != null)
            .And(c => c.Description != null)
            .OrderBy(c => c.CategoryName)
            .Take(3);

        var products = _fixture.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        Assert.NotEmpty(products);
    }
}
