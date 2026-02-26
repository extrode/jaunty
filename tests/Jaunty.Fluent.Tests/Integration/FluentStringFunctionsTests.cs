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

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName!.Length > 10));
    }

    [Fact]
    public void Length_ToSql_GeneratesLengthFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length > 10)
            .ToSql();

        // SQLite uses LENGTH
        Assert.Contains("LENGTH", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void Length_WithEquality_FiltersCorrectly()
    {
        // Products with names exactly 4 characters
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length == 4)
            .Select();

        Assert.All(products, p => Assert.True(p.ProductName!.Length == 4));
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

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName!.Equals("Chang", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ToUpper_ToSql_GeneratesUpperFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToUpper() == "CHANG")
            .ToSql();

        Assert.Contains("UPPER", sql);
        Assert.Contains("product_name", sql);
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

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName!.Equals("Chang", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ToLower_ToSql_GeneratesLowerFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToLower() == "chang")
            .ToSql();

        Assert.Contains("LOWER", sql);
        Assert.Contains("product_name", sql);
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

        Assert.Contains("TRIM", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void Trim_InWhere_WorksCorrectly()
    {
        // Trim shouldn't affect normal product names (no leading/trailing spaces)
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Trim() == "Chang")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName!.Trim() == "Chang"));
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
        Assert.Contains("SUBSTR", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void Substring_InWhere_FiltersResults()
    {
        // Find products starting with "Cha"
        var products = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Substring(0, 3) == "Cha")
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName!.Substring(0, 3) == "Cha"));
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

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True(p.ProductName!.Length > 5 &&
            p.Discontinued == false));
    }

    [Fact]
    public void ToUpper_CombinedWithOr_FiltersCorrectly()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToUpper() == "CHANG")
            .Or(p => p.ProductName.ToUpper() == "CHANG")
            .ToSql();

        Assert.Contains("UPPER", sql);
        Assert.Contains("OR", sql);
    }

    [Fact]
    public void Multiple_StringFunctions_InSameQuery()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Length > 5)
            .And(p => p.ProductName.ToUpper() != "TEST")
            .ToSql();

        Assert.Contains("LENGTH", sql);
        Assert.Contains("UPPER", sql);
        Assert.Contains("AND", sql);
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

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True(p.ProductName!.Length > 10));
    }

    [Fact]
    public async Task ToUpper_CountAsync_ReturnsFilteredCount()
    {
        var count = await _db.Connection.From<Product>()
            .Where(p => p.ProductName.ToUpper() == "CHANG")
            .CountAsync();

        Assert.True(count > 0);
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

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True(string.Compare(products[i - 1].ProductName, products[i].ProductName) <= 0);
        }
    }

    [Fact]
    public void Substring_WithDifferentPositions()
    {
        // Test substring starting at position 1 (C# 0-based)
        var sql = _db.Connection.From<Product>()
            .Where(p => p.ProductName.Substring(1, 2) == "ha")
            .ToSql();

        Assert.Contains("SUBSTR", sql);
        Assert.Contains("product_name", sql);
    }

    [Fact]
    public void StringFunctions_WithNullableColumn()
    {
        // QuantityPerUnit is a nullable string column
        var sql = _db.Connection.From<Product>()
            .Where(p => p.QuantityPerUnit!.Length > 5)
            .ToSql();

        Assert.Contains("LENGTH", sql);
        Assert.Contains("quantity_per_unit", sql);
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

        Assert.Contains("COALESCE", sql);
    }

    [Fact]
    public void SqlYear_StillWorks()
    {
        var sql = _db.Connection.From<Order>()
            .Where(o => Sql.Year(o.OrderDate) == 1997)
            .ToSql();

        Assert.Contains("order_date", sql);
    }
}
