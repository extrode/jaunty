using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentExistsTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentExistsTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // WhereExists Tests
    // ==========================================

    [Fact]
    public void WhereExists_FindsCategoriesWithProducts()
    {
        // Find categories that have at least one product
        var categories = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        Assert.NotEmpty(categories);

        // All returned categories should have products
        foreach (var category in categories)
        {
            var productCount = _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == category.CategoryId)
                .Count();
            Assert.True(productCount > 0);
        }
    }

    [Fact]
    public void WhereExists_ToSql_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        Assert.Contains("EXISTS", sql);
        Assert.Contains("SELECT 1 FROM", sql);
        Assert.Contains("products", sql);
        Assert.Contains("WHERE", sql);
        Assert.Contains("category_id", sql);
    }

    // ==========================================
    // WhereNotExists Tests
    // ==========================================

    [Fact]
    public void WhereNotExists_FindsCategoriesWithoutProducts()
    {
        // Find categories that have no products
        var categoriesWithNoProducts = _fixture.Connection.From<Category>()
            .WhereNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        // In Northwind, all categories should have products, so this might be empty
        // But the SQL should be valid and execute without errors
        foreach (var category in categoriesWithNoProducts)
        {
            var productCount = _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == category.CategoryId)
                .Count();
            Assert.Equal(0, productCount);
        }
    }

    [Fact]
    public void WhereNotExists_ToSql_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Category>()
            .WhereNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("SELECT 1 FROM", sql);
        Assert.Contains("products", sql);
    }

    // ==========================================
    // AndExists / AndNotExists Tests
    // ==========================================

    [Fact]
    public void AndExists_CombinesWithWhere()
    {
        // Categories that start with 'C' and have products
        var categories = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName!.StartsWith("C"))
            .AndExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        Assert.NotEmpty(categories);
        Assert.All(categories, c => Assert.True(c.CategoryName!.StartsWith("C")));
    }

    [Fact]
    public void AndNotExists_CombinesWithWhere()
    {
        var sql = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryId > 0)
            .AndNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        Assert.Contains("WHERE", sql);
        Assert.Contains("AND NOT EXISTS", sql);
    }

    // ==========================================
    // OrExists / OrNotExists Tests
    // ==========================================

    [Fact]
    public void OrExists_CombinesWithWhere()
    {
        // Categories named "Beverages" or categories that have products with high stock
        var sql = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryName == "Beverages")
            .OrExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.UnitsInStock > 100)
            .ToSql();

        Assert.Contains("OR EXISTS", sql);
        Assert.Contains("units_in_stock", sql);
    }

    [Fact]
    public void OrNotExists_ToSql_GeneratesCorrectSql()
    {
        var sql = _fixture.Connection.From<Category>()
            .Where(c => c.CategoryId == 1)
            .OrNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        Assert.Contains("OR NOT EXISTS", sql);
    }

    // ==========================================
    // Complex Predicate Tests
    // ==========================================

    [Fact]
    public void WhereExists_WithComplexPredicate_FiltersCorrectly()
    {
        // Find categories that have discontinued products
        var categoriesWithDiscontinued = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.Discontinued == true)
            .Select();

        // Verify each returned category has at least one discontinued product
        foreach (var category in categoriesWithDiscontinued)
        {
            var discontinuedCount = _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == category.CategoryId)
                .And(p => p.Discontinued == true)
                .Count();
            Assert.True(discontinuedCount > 0);
        }
    }

    [Fact]
    public void WhereExists_WithMultipleConditions_ToSql()
    {
        var sql = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.UnitPrice > 50 && p.Discontinued == false)
            .ToSql();

        Assert.Contains("EXISTS", sql);
        Assert.Contains("category_id", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains("discontinued", sql);
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task WhereExists_SelectAsync_ReturnsCorrectResults()
    {
        var categories = await _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .SelectAsync();

        Assert.NotEmpty(categories);
    }

    [Fact]
    public async Task WhereNotExists_CountAsync_ReturnsValidCount()
    {
        var count = await _fixture.Connection.From<Category>()
            .WhereNotExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .CountAsync();

        // Northwind's seed data has at least one product per category, so no category should
        // satisfy WhereNotExists; a regression that inverted the clause (e.g. behaved like
        // WhereExists) would return a non-zero count instead.
        Assert.Equal(0, count);
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void WhereExists_WithOrderBy_WorksCorrectly()
    {
        var categories = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .OrderBy(c => c.CategoryName)
            .Select();

        Assert.NotEmpty(categories);
        for (int i = 1; i < categories.Count; i++)
        {
            Assert.True(string.Compare(categories[i - 1].CategoryName, categories[i].CategoryName) <= 0);
        }
    }

    [Fact]
    public void WhereExists_WithTake_LimitsResults()
    {
        var categories = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Take(3)
            .Select();

        Assert.True(categories.Count <= 3);
    }

    // ==========================================
    // Alias / Self-Reference / Null Handling
    // ==========================================

    [Fact]
    public void WhereExists_WithOuterAlias_UsesAliasNotTableName()
    {
        var sql = _fixture.Connection.From<Category>("c")
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        // The outer correlation column must be prefixed with the alias the query was
        // created with, not the raw (escaped) table name.
        Assert.Contains("c.category_id", sql);
        Assert.DoesNotContain("categories.category_id", sql);
    }

    // AUD-R35: the assertion above is text-only and passed while the statement was unrunnable -
    // the alias was referenced and never declared. These execute it.
    [Fact]
    public void WhereExists_WithOuterAlias_DeclaresTheAliasOnTheOuterTable()
    {
        var sql = _fixture.Connection.From<Category>("c")
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .ToSql();

        Assert.Contains("categories c WHERE", sql);
    }

    [Fact]
    public void WhereExists_WithOuterAlias_Executes()
    {
        var aliased = _fixture.Connection.From<Category>("c")
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        var unaliased = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Select();

        Assert.NotEmpty(aliased);
        Assert.Equal(unaliased.Count, aliased.Count);
    }

    [Fact]
    public void WhereExists_WithOuterAlias_CountAndAggregateExecute()
    {
        int count = _fixture.Connection.From<Category>("c")
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Count();

        int max = _fixture.Connection.From<Category>("c")
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
            .Max(c => c.CategoryId);

        Assert.True(count > 0);
        Assert.True(max > 0);
    }

    [Fact]
    public void WhereExists_WithOuterAlias_DeleteThrowsRatherThanNamingAnUndeclaredAlias()
    {
        NotSupportedException ex = Assert.Throws<NotSupportedException>(() =>
            _fixture.Connection.From<Category>("c")
                .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId)
                .Delete());
        Assert.Contains("cannot declare a table alias", ex.Message);
    }

    [Theory]
    [InlineData("c\"; DROP TABLE categories --")]
    [InlineData("a b")]
    [InlineData("")]
    public void From_InvalidAlias_IsRejected(string alias)
    {
        Assert.Throws<ArgumentException>(() => _fixture.Connection.From<Category>(alias));
    }

    [Fact]
    public void WhereExists_SelfReferencing_UsesDistinctAliasesForBothSides()
    {
        // TOuter == TSubquery (Product self-referencing EXISTS): both sides must not collapse
        // onto the same table prefix, or the correlation would be meaningless.
        var sql = _fixture.Connection.From<Product>()
            .WhereExists<Product>((p1, p2) => p1.CategoryId == p2.CategoryId && p1.ProductId != p2.ProductId)
            .ToSql();

        Assert.Contains("products_ex", sql);
    }

    [Fact]
    public void WhereExists_SelfReferencing_FindsProductsWithSiblingInSameCategory()
    {
        var products = _fixture.Connection.From<Product>()
            .WhereExists<Product>((p1, p2) => p1.CategoryId == p2.CategoryId && p1.ProductId != p2.ProductId)
            .Select();

        Assert.NotEmpty(products);

        foreach (var product in products)
        {
            var siblingCount = _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == product.CategoryId)
                .And(p => p.ProductId != product.ProductId)
                .Count();
            Assert.True(siblingCount > 0);
        }
    }

    [Fact]
    public void WhereExists_ComparisonAgainstNull_GeneratesIsNull()
    {
        var sql = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.SupplierId == null)
            .ToSql();

        Assert.Contains("IS NULL", sql);
        Assert.DoesNotContain("= NULL", sql);
    }

    [Fact]
    public void WhereExists_ComparisonAgainstNull_FiltersCorrectly()
    {
        // Products with no supplier: correlate against that so only categories that have a
        // supplier-less product are returned. Confirms IS NULL (not "= NULL", which is always
        // UNKNOWN/false under SQL's three-valued logic) is actually being applied.
        var categories = _fixture.Connection.From<Category>()
            .WhereExists<Product>((c, p) => c.CategoryId == p.CategoryId && p.SupplierId == null)
            .Select();

        foreach (var category in categories)
        {
            var matchCount = _fixture.Connection.From<Product>()
                .Where(p => p.CategoryId == category.CategoryId)
                .And(p => p.SupplierId == null)
                .Count();
            Assert.True(matchCount > 0);
        }
    }
}