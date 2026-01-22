using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentStringFunctionsTests : IDisposable
{
    private readonly Database _db;

    public FluentStringFunctionsTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // string.Length Tests
    // ==========================================

    [Fact]
    public void Length_InWhere_FiltersResults()
    {
        // Products with names longer than 10 characters
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length > 10)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Length > 10);
    }

    [Fact]
    public void Length_ToSql_GeneratesLengthFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length > 10)
            .ToSql();

        // SQLite uses LENGTH
        sql.Should().Contain("LENGTH");
        sql.Should().Contain("product_name");
    }

    [Fact]
    public void Length_WithEquality_FiltersCorrectly()
    {
        // Products with names exactly 4 characters
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length == 4)
            .Select();

        products.Should().OnlyContain(p => p.ProductName!.Length == 4);
    }

    // ==========================================
    // string.ToUpper Tests
    // ==========================================

    [Fact]
    public void ToUpper_InWhere_FiltersResults()
    {
        // Find product "Chang" using uppercase comparison
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToUpper() == "CHANG")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Equals("Chang", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ToUpper_ToSql_GeneratesUpperFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToUpper() == "CHANG")
            .ToSql();

        sql.Should().Contain("UPPER");
        sql.Should().Contain("product_name");
    }

    // ==========================================
    // string.ToLower Tests
    // ==========================================

    [Fact]
    public void ToLower_InWhere_FiltersResults()
    {
        // Find product "Chang" using lowercase comparison
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToLower() == "chang")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Equals("Chang", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ToLower_ToSql_GeneratesLowerFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToLower() == "chang")
            .ToSql();

        sql.Should().Contain("LOWER");
        sql.Should().Contain("product_name");
    }

    // ==========================================
    // string.Trim Tests
    // ==========================================

    [Fact]
    public void Trim_ToSql_GeneratesTrimFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Trim() == "Chang")
            .ToSql();

        sql.Should().Contain("TRIM");
        sql.Should().Contain("product_name");
    }

    [Fact]
    public void Trim_InWhere_WorksCorrectly()
    {
        // Trim shouldn't affect normal product names (no leading/trailing spaces)
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Trim() == "Chang")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Trim() == "Chang");
    }

    // ==========================================
    // string.Substring Tests
    // ==========================================

    [Fact]
    public void Substring_ToSql_GeneratesSubstringFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Substring(0, 3) == "Cha")
            .ToSql();

        // SQLite uses SUBSTR
        sql.Should().Contain("SUBSTR");
        sql.Should().Contain("product_name");
    }

    [Fact]
    public void Substring_InWhere_FiltersResults()
    {
        // Find products starting with "Cha"
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Substring(0, 3) == "Cha")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Substring(0, 3) == "Cha");
    }

    // ==========================================
    // Combined Tests
    // ==========================================

    [Fact]
    public void Length_CombinedWithAnd_FiltersCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length > 5)
            .And(p => p.Discontinued == false)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.ProductName!.Length > 5 &&
            p.Discontinued == false);
    }

    [Fact]
    public void ToUpper_CombinedWithOr_FiltersCorrectly()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToUpper() == "CHANG")
            .Or(p => p.ProductName.ToUpper() == "CHANG")
            .ToSql();

        sql.Should().Contain("UPPER");
        sql.Should().Contain("OR");
    }

    [Fact]
    public void Multiple_StringFunctions_InSameQuery()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length > 5)
            .And(p => p.ProductName.ToUpper() != "TEST")
            .ToSql();

        sql.Should().Contain("LENGTH");
        sql.Should().Contain("UPPER");
        sql.Should().Contain("AND");
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task Length_SelectAsync_FiltersCorrectly()
    {
        var products = await _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length > 10)
            .SelectAsync();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Length > 10);
    }

    [Fact]
    public async Task ToUpper_CountAsync_ReturnsFilteredCount()
    {
        var count = await _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToUpper() == "CHANG")
            .CountAsync();

        count.Should().BeGreaterThan(0);
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void Length_WithOrderBy_WorksCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length > 5)
            .OrderBy(p => p.ProductName)
            .Take(5)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void Substring_WithDifferentPositions()
    {
        // Test substring starting at position 1 (C# 0-based)
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Substring(1, 2) == "ha")
            .ToSql();

        sql.Should().Contain("SUBSTR");
        sql.Should().Contain("product_name");
    }

    [Fact]
    public void StringFunctions_WithNullableColumn()
    {
        // QuantityPerUnit is a nullable string column
        var sql = _db.Connection.From<Product>()
            .Where(p => p.QuantityPerUnit!.Length > 5)
            .ToSql();

        sql.Should().Contain("LENGTH");
        sql.Should().Contain("quantity_per_unit");
    }

    // ==========================================
    // Sql.* still works for functions without C# equivalents
    // ==========================================

    [Fact]
    public void SqlCoalesce_StillWorks()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.QuantityPerUnit, "N/A") != "N/A")
            .ToSql();

        sql.Should().Contain("COALESCE");
    }

    [Fact]
    public void SqlYear_StillWorks()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .ToSql();

        sql.Should().Contain("order_date");
    }
}
