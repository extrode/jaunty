using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for subquery support (IN subquery, NOT IN subquery).
/// </summary>
public class FluentSubqueryTests : IDisposable
{
    private readonly Database _db;

    public FluentSubqueryTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // SQL Generation Tests
    // ==========================================

    [Fact]
    public void WhereInSubquery_ToSql_GeneratesCorrectSyntax()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages");

        var sql = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .ToSql();

        sql.Should().Contain("IN (SELECT");
        sql.Should().Contain("category_id");
        sql.Should().Contain("categories");
    }

    [Fact]
    public void WhereNotInSubquery_ToSql_GeneratesCorrectSyntax()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages");

        var sql = _db.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .ToSql();

        sql.Should().Contain("NOT IN (SELECT");
        sql.Should().Contain("category_id");
    }

    [Fact]
    public void AndInSubquery_ToSql_GeneratesCorrectSyntax()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages");

        var sql = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .ToSql();

        sql.Should().Contain("discontinued");
        sql.Should().Contain("AND");
        sql.Should().Contain("IN (SELECT");
    }

    [Fact]
    public void OrInSubquery_ToSql_GeneratesCorrectSyntax()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages");

        var sql = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == 99)
            .OrInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .ToSql();

        sql.Should().Contain("category_id = @");
        sql.Should().Contain("OR");
        sql.Should().Contain("IN (SELECT");
    }

    // ==========================================
    // Query Execution Tests - WhereInSubquery
    // ==========================================

    [Fact]
    public void WhereInSubquery_FiltersByCategory_ReturnsCorrectProducts()
    {
        // Get categories that start with 'B' (Beverages, etc.)
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("B"));

        var products = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        products.Should().NotBeEmpty();

        // Verify all products are in categories starting with 'B'
        var validCategoryIds = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("B"))
            .Select()
            .Select(c => c.CategoryId)
            .ToHashSet();

        products.Should().OnlyContain(p => validCategoryIds.Contains(p.CategoryId!.Value));
    }

    [Fact]
    public void WhereInSubquery_WithMultipleConditions_ReturnsCorrectProducts()
    {
        // Subquery: categories that have description
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.Description != null);

        var products = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        products.Should().NotBeEmpty();
    }

    // ==========================================
    // Query Execution Tests - WhereNotInSubquery
    // ==========================================

    [Fact]
    public void WhereNotInSubquery_ExcludesMatchingProducts()
    {
        // Get the first category
        var firstCategory = _db.Connection.From<Category>()
            .Take(1)
            .SelectFirst();

        // Subquery: select that category's ID
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryId == firstCategory.CategoryId);

        var products = _db.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId != firstCategory.CategoryId);
    }

    [Fact]
    public void WhereNotInSubquery_WithNoMatchingSubquery_ReturnsAllProducts()
    {
        // Subquery: categories with impossible name
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName == "NonExistent12345");

        var productsWithNotIn = _db.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        var allProducts = _db.Connection.From<Product>().Select();

        productsWithNotIn.Should().HaveCount(allProducts.Count);
    }

    // ==========================================
    // Query Execution Tests - AndInSubquery / OrInSubquery
    // ==========================================

    [Fact]
    public void AndInSubquery_CombinesWithPreviousCondition()
    {
        // Subquery: categories starting with 'C'
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        // Get valid category IDs
        var validCategoryIds = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"))
            .Select()
            .Select(c => c.CategoryId)
            .ToHashSet();

        products.Should().OnlyContain(p =>
            p.Discontinued == false && validCategoryIds.Contains(p.CategoryId!.Value));
    }

    [Fact]
    public void AndNotInSubquery_CombinesWithPreviousCondition()
    {
        // Get first category
        var firstCategory = _db.Connection.From<Category>()
            .Take(1)
            .SelectFirst();

        // Subquery: the first category
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryId == firstCategory.CategoryId);

        var products = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == false)
            .AndNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        products.Should().OnlyContain(p =>
            p.Discontinued == false && p.CategoryId != firstCategory.CategoryId);
    }

    [Fact]
    public void OrInSubquery_ProvidersAlternativeMatch()
    {
        // Get first two categories
        var categories = _db.Connection.From<Category>()
            .Take(2)
            .Select();

        var cat1Id = categories[0].CategoryId;
        var cat2Id = categories.Count > 1 ? categories[1].CategoryId : cat1Id;

        // Subquery: second category
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryId == cat2Id);

        var products = _db.Connection.From<Product>()
            .Where(p => p.CategoryId == cat1Id)
            .OrInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        products.Should().OnlyContain(p =>
            p.CategoryId == cat1Id || p.CategoryId == cat2Id);
    }

    [Fact]
    public void OrNotInSubquery_ProvidersAlternativeMatch()
    {
        // Get first category
        var firstCategory = _db.Connection.From<Category>()
            .Take(1)
            .SelectFirst();

        // Subquery: the first category
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryId == firstCategory.CategoryId);

        // Products that are discontinued OR not in first category
        var products = _db.Connection.From<Product>()
            .Where(p => p.Discontinued == true)
            .OrNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        products.Should().OnlyContain(p =>
            p.Discontinued == true || p.CategoryId != firstCategory.CategoryId);
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task WhereInSubquery_SelectAsync_ReturnsCorrectProducts()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = await _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .SelectAsync();

        products.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WhereInSubquery_CountAsync_ReturnsCorrectCount()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var count = await _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .CountAsync();

        count.Should().BeGreaterThan(0);
    }

    // ==========================================
    // Combined with Other Features
    // ==========================================

    [Fact]
    public void WhereInSubquery_WithOrderBy_WorksCorrectly()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .OrderBy(p => p.ProductName)
            .Select();

        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void WhereInSubquery_WithTake_LimitsResults()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Take(3)
            .Select();

        products.Should().HaveCountLessThanOrEqualTo(3);
    }

    [Fact]
    public void WhereInSubquery_WithDistinct_ReturnsUniqueResults()
    {
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("C"));

        var products = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        // All product IDs should be unique
        var productIds = products.Select(p => p.ProductId).ToList();
        productIds.Should().OnlyHaveUniqueItems();
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void WhereInSubquery_SubqueryReturnsNoRows_ReturnsNoResults()
    {
        // Subquery that returns no results
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName == "NonExistentCategory12345");

        var products = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        products.Should().BeEmpty();
    }

    [Fact]
    public void WhereNotInSubquery_SubqueryReturnsNoRows_ReturnsAllResults()
    {
        // Subquery that returns no results
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName == "NonExistentCategory12345");

        var products = _db.Connection.From<Product>()
            .WhereNotInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        var allProducts = _db.Connection.From<Product>().Select();
        products.Should().HaveCount(allProducts.Count);
    }

    [Fact]
    public void WhereInSubquery_ChainedSubqueries_WorksCorrectly()
    {
        // First subquery
        var subquery1 = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("B"));

        // Use first subquery in main query, then add another condition
        var products = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery1)
            .And(p => p.Discontinued == false)
            .Select();

        var validCategoryIds = _db.Connection.From<Category>()
            .Where(c => c.CategoryName.StartsWith("B"))
            .Select()
            .Select(c => c.CategoryId)
            .ToHashSet();

        products.Should().OnlyContain(p =>
            validCategoryIds.Contains(p.CategoryId!.Value) && p.Discontinued == false);
    }

    [Fact]
    public void WhereInSubquery_ComplexSubquery_WorksCorrectly()
    {
        // Complex subquery with multiple conditions
        var subquery = _db.Connection.From<Category>()
            .Where(c => c.CategoryName != null)
            .And(c => c.Description != null)
            .OrderBy(c => c.CategoryName)
            .Take(3);

        var products = _db.Connection.From<Product>()
            .WhereInSubquery<int, Category>(
                p => p.CategoryId!.Value,
                c => c.CategoryId,
                subquery)
            .Select();

        products.Should().NotBeEmpty();
    }
}
