using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

/// <summary>
/// Tests for Sql.Case() CASE/WHEN expressions.
/// </summary>
public class FluentCaseWhenTests : IDisposable
{
    private readonly Database _db;

    public FluentCaseWhenTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // Basic CASE/WHEN SQL Generation Tests
    // ==========================================

    [Fact]
    public void Case_When_Else_ToSql_GeneratesCorrectCaseSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.UnitsInStock > 100, "High")
                .When(x => x.UnitsInStock > 50, "Medium")
                .Else("Low") == "High")
            .ToSql();

        sql.Should().Contain("CASE");
        sql.Should().Contain("WHEN");
        sql.Should().Contain("THEN");
        sql.Should().Contain("ELSE");
        sql.Should().Contain("END");
    }

    [Fact]
    public void Case_SingleWhen_Else_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.Discontinued == true, "Discontinued")
                .Else("Active") == "Discontinued")
            .ToSql();

        sql.Should().Contain("CASE WHEN");
        sql.Should().Contain("THEN");
        sql.Should().Contain("ELSE");
        sql.Should().Contain("END");
    }

    [Fact]
    public void Case_MultipleWhen_ToSql_ContainsMultipleWhenClauses()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, int>()
                .When(x => x.CategoryId == 1, 100)
                .When(x => x.CategoryId == 2, 200)
                .When(x => x.CategoryId == 3, 300)
                .Else(0) > 0)
            .ToSql();

        // Count occurrences of WHEN
        var whenCount = sql.Split(new[] { "WHEN" }, StringSplitOptions.None).Length - 1;
        whenCount.Should().Be(3);
    }

    [Fact]
    public void Case_End_WithoutElse_ToSql_GeneratesCorrectSyntax()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.Discontinued == true, "Discontinued")
                .End() == "Discontinued")
            .ToSql();

        sql.Should().Contain("CASE WHEN");
        sql.Should().Contain("THEN");
        sql.Should().Contain("END");
        sql.Should().NotContain("ELSE");
    }

    // ==========================================
    // Query Execution Tests
    // ==========================================

    [Fact]
    public void Case_When_FiltersByCategory_ExecutesCorrectly()
    {
        // Filter products where CASE expression equals "Beverages"
        // Category 1 is typically Beverages in Northwind
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Beverages")
                .Else("Other") == "Beverages")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public void Case_When_WithNumericComparison_ExecutesCorrectly()
    {
        // Products with high stock (> 100) categorized as "High"
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.UnitsInStock > 100, "High")
                .Else("Normal") == "High")
            .Select();

        products.Should().OnlyContain(p => p.UnitsInStock > 100);
    }

    [Fact]
    public void Case_When_WithBooleanCondition_ExecutesCorrectly()
    {
        // Find discontinued products using CASE
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, bool>()
                .When(x => x.Discontinued == true, true)
                .Else(false) == true)
            .Select();

        products.Should().OnlyContain(p => p.Discontinued == true);
    }

    [Fact]
    public void Case_When_MultipleCriteria_ExecutesCorrectly()
    {
        // Products in category 1 or 2 based on CASE expression returning 1
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, int>()
                .When(x => x.CategoryId == 1, 1)
                .When(x => x.CategoryId == 2, 1)
                .Else(0) == 1)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1 || p.CategoryId == 2);
    }

    // ==========================================
    // Combined with Other Features Tests
    // ==========================================

    [Fact]
    public void Case_When_CombinedWithAnd_ExecutesCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Beverages")
                .Else("Other") == "Beverages")
            .And(p => p.Discontinued == false)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1 && p.Discontinued == false);
    }

    [Fact]
    public void Case_When_CombinedWithOrderBy_ExecutesCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Match")
                .Else("NoMatch") == "Match")
            .OrderBy(p => p.ProductName)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void Case_When_CombinedWithTake_ExecutesCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Match")
                .Else("NoMatch") == "Match")
            .Take(5)
            .Select();

        products.Should().HaveCountLessThanOrEqualTo(5);
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task Case_When_SelectAsync_ExecutesCorrectly()
    {
        var products = await _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Beverages")
                .Else("Other") == "Beverages")
            .SelectAsync();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1);
    }

    [Fact]
    public async Task Case_When_CountAsync_ExecutesCorrectly()
    {
        var count = await _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, int>()
                .When(x => x.CategoryId == 1, 1)
                .Else(0) == 1)
            .CountAsync();

        count.Should().BeGreaterThan(0);
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void Case_When_WithNullableColumn_ExecutesCorrectly()
    {
        // Test with nullable column (UnitsInStock is nullable short)
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.UnitsInStock == null, "NoStock")
                .When(x => x.UnitsInStock == 0, "Empty")
                .Else("HasStock") == "HasStock")
            .Select();

        // Products with some stock
        products.Should().OnlyContain(p => p.UnitsInStock != null && p.UnitsInStock != 0);
    }

    [Fact]
    public void Case_When_NotEquals_ExecutesCorrectly()
    {
        // Products NOT in category 1
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, string>()
                .When(x => x.CategoryId == 1, "Beverages")
                .Else("Other") != "Beverages")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId != 1);
    }

    [Fact]
    public void Case_When_GreaterThan_ExecutesCorrectly()
    {
        // Products with CASE score > 50
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Case<Product, int>()
                .When(x => x.CategoryId == 1, 100)
                .When(x => x.CategoryId == 2, 75)
                .Else(25) > 50)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.CategoryId == 1 || p.CategoryId == 2);
    }
}
