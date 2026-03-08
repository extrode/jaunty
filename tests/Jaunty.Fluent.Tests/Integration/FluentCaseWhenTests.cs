using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Sql.Case() CASE/WHEN expressions.
/// </summary>
public class FluentCaseWhenTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentCaseWhenTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // Basic CASE/WHEN SQL Generation Tests
    // ==========================================

    [Fact]
    public void Case_When_Else_ToSql_GeneratesCorrectCaseSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.UnitsInStock > 100, "High")
                .When(x => x.UnitsInStock > 50, "Medium")
                .Else("Low") == "High")
            .ToSql();

        Assert.Contains("CASE", sql);
        Assert.Contains("WHEN", sql);
        Assert.Contains("THEN", sql);
        Assert.Contains("ELSE", sql);
        Assert.Contains("END", sql);
    }

    [Fact]
    public void Case_SingleWhen_Else_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.Discontinued == true, "Discontinued")
                .Else("Active") == "Discontinued")
            .ToSql();

        Assert.Contains("CASE WHEN", sql);
        Assert.Contains("THEN", sql);
        Assert.Contains("ELSE", sql);
        Assert.Contains("END", sql);
    }

    [Fact]
    public void Case_MultipleWhen_ToSql_ContainsMultipleWhenClauses()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, int>()
                .When(x => x.CategoryId == 1, 100)
                .When(x => x.CategoryId == 2, 200)
                .When(x => x.CategoryId == 3, 300)
                .Else(0) > 0)
            .ToSql();

        // Count occurrences of WHEN
        var whenCount = sql.Split(new[] { "WHEN" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(3, whenCount);
    }

    [Fact]
    public void Case_End_WithoutElse_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.Discontinued == true, "Discontinued")
                .End() == "Discontinued")
            .ToSql();

        Assert.Contains("CASE WHEN", sql);
        Assert.Contains("THEN", sql);
        Assert.Contains("END", sql);
        Assert.DoesNotContain("ELSE", sql);
    }

    // ==========================================
    // Query Execution Tests
    // ==========================================

    [Fact]
    public void Case_When_FiltersByCategory_ExecutesCorrectly()
    {
        // Filter products where CASE expression equals "Beverages"
        // Category 1 is typically Beverages in Northwind
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Beverages")
                .Else("Other") == "Beverages")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public void Case_When_WithNumericComparison_ExecutesCorrectly()
    {
        // Products with high stock (> 100) categorized as "High"
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.UnitsInStock > 100, "High")
                .Else("Normal") == "High")
            .Select();

        Assert.All(products, p => Assert.True(p.UnitsInStock > 100));
    }

    [Fact]
    public void Case_When_WithBooleanCondition_ExecutesCorrectly()
    {
        // Find discontinued products using CASE
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, bool>()
                .When(x => x.Discontinued == true, true)
                .Else(false) == true)
            .Select();

        Assert.All(products, p => Assert.True(p.Discontinued == true));
    }

    [Fact]
    public void Case_When_MultipleCriteria_ExecutesCorrectly()
    {
        // Products in category 1 or 2 based on CASE expression returning 1
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, int>()
                .When(x => x.CategoryId == 1, 1)
                .When(x => x.CategoryId == 2, 1)
                .Else(0) == 1)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }

    // ==========================================
    // Combined with Other Features Tests
    // ==========================================

    [Fact]
    public void Case_When_CombinedWithAnd_ExecutesCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Beverages")
                .Else("Other") == "Beverages")
            .And(p => p.Discontinued == false)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 && p.Discontinued == false));
    }

    [Fact]
    public void Case_When_CombinedWithOrderBy_ExecutesCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Match")
                .Else("NoMatch") == "Match")
            .OrderBy(p => p.ProductName)
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void Case_When_CombinedWithTake_ExecutesCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Match")
                .Else("NoMatch") == "Match")
            .Take(5)
            .Select();

        Assert.True(products.Count <= 5);
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task Case_When_SelectAsync_ExecutesCorrectly()
    {
        var products = await _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Beverages")
                .Else("Other") == "Beverages")
            .SelectAsync();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal((short)1, p.CategoryId));
    }

    [Fact]
    public async Task Case_When_CountAsync_ExecutesCorrectly()
    {
        var count = await _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, int>()
                .When(x => x.CategoryId == 1, 1)
                .Else(0) == 1)
            .CountAsync();

        Assert.True(count > 0);
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void Case_When_WithNullableColumn_ExecutesCorrectly()
    {
        // Test with nullable column (UnitsInStock is nullable short)
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.UnitsInStock == null, "NoStock")
                .When(x => x.UnitsInStock == 0, "Empty")
                .Else("HasStock") == "HasStock")
            .Select();

        // Products with some stock
        Assert.All(products, p => Assert.True(p.UnitsInStock != null && p.UnitsInStock != 0));
    }

    [Fact]
    public void Case_When_NotEquals_ExecutesCorrectly()
    {
        // Products NOT in category 1
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Beverages")
                .Else("Other") != "Beverages")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId != 1));
    }

    [Fact]
    public void Case_When_GreaterThan_ExecutesCorrectly()
    {
        // Products with CASE score > 50
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Case<Product, int>()
                .When(x => x.CategoryId == 1, 100)
                .When(x => x.CategoryId == 2, 75)
                .Else(25) > 50)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.CategoryId == 1 || p.CategoryId == 2));
    }
}