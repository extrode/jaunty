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
    // Sql.Length Tests
    // ==========================================

    [Fact]
    public void Length_InWhere_FiltersResults()
    {
        // Products with names longer than 10 characters
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Length(p.ProductName) > 10)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Length > 10);
    }

    [Fact]
    public void Length_ToSql_GeneratesLengthFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Length(p.ProductName) > 10)
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
            .Where(p => Sql.Length(p.ProductName) == 4)
            .Select();

        products.Should().OnlyContain(p => p.ProductName!.Length == 4);
    }

    // ==========================================
    // Sql.Upper Tests
    // ==========================================

    [Fact]
    public void Upper_InWhere_FiltersResults()
    {
        // Find product "Chai" using uppercase comparison
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Upper(p.ProductName) == "CHAI")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Equals("Chai", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Upper_ToSql_GeneratesUpperFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Upper(p.ProductName) == "CHAI")
            .ToSql();

        sql.Should().Contain("UPPER");
        sql.Should().Contain("product_name");
    }

    // ==========================================
    // Sql.Lower Tests
    // ==========================================

    [Fact]
    public void Lower_InWhere_FiltersResults()
    {
        // Find product "Chai" using lowercase comparison
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Lower(p.ProductName) == "chai")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Equals("Chai", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Lower_ToSql_GeneratesLowerFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Lower(p.ProductName) == "chai")
            .ToSql();

        sql.Should().Contain("LOWER");
        sql.Should().Contain("product_name");
    }

    // ==========================================
    // Sql.Trim Tests
    // ==========================================

    [Fact]
    public void Trim_ToSql_GeneratesTrimFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Trim(p.ProductName) == "Chai")
            .ToSql();

        sql.Should().Contain("TRIM");
        sql.Should().Contain("product_name");
    }

    [Fact]
    public void Trim_InWhere_WorksCorrectly()
    {
        // Trim shouldn't affect normal product names (no leading/trailing spaces)
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Trim(p.ProductName) == "Chai")
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Trim() == "Chai");
    }

    // ==========================================
    // Sql.Substring Tests
    // ==========================================

    [Fact]
    public void Substring_ToSql_GeneratesSubstringFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Substring(p.ProductName, 1, 3) == "Cha")
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
            .Where(p => Sql.Substring(p.ProductName, 1, 3) == "Cha")
            .Select();

        products.Should().NotBeEmpty();
        // SQLite SUBSTR uses 1-based indexing
        products.Should().OnlyContain(p => p.ProductName!.Substring(0, 3) == "Cha");
    }

    // ==========================================
    // Combined Tests
    // ==========================================

    [Fact]
    public void Length_CombinedWithAnd_FiltersCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Length(p.ProductName) > 5)
            .And(p => p.Discontinued == false)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            p.ProductName!.Length > 5 &&
            p.Discontinued == false);
    }

    [Fact]
    public void Upper_CombinedWithOr_FiltersCorrectly()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Upper(p.ProductName) == "CHAI")
            .Or(p => Sql.Upper(p.ProductName) == "CHANG")
            .ToSql();

        sql.Should().Contain("UPPER");
        sql.Should().Contain("OR");
    }

    [Fact]
    public void Multiple_StringFunctions_InSameQuery()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Length(p.ProductName) > 5)
            .And(p => Sql.Upper(p.ProductName) != "TEST")
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
            .Where(p => Sql.Length(p.ProductName) > 10)
            .SelectAsync();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => p.ProductName!.Length > 10);
    }

    [Fact]
    public async Task Upper_CountAsync_ReturnsFilteredCount()
    {
        var count = await _db.Connection.From<Product>()
            .Where(p => Sql.Upper(p.ProductName) == "CHAI")
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
            .Where(p => Sql.Length(p.ProductName) > 5)
            .OrderBy(p => p.ProductName)
            .Take(5)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.ProductName);
    }

    [Fact]
    public void Substring_WithDifferentPositions()
    {
        // Test substring starting at position 2
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Substring(p.ProductName, 2, 2) == "ha")
            .ToSql();

        // Values are parameterized, not inlined
        sql.Should().Contain("SUBSTR");
        sql.Should().Contain("product_name");
        sql.Should().Contain("@SqlFn");  // start position parameter
    }

    [Fact]
    public void StringFunctions_WithNullableColumn()
    {
        // QuantityPerUnit is a nullable string column
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Length(p.QuantityPerUnit) > 5)
            .ToSql();

        sql.Should().Contain("LENGTH");
        sql.Should().Contain("quantity_per_unit");
    }
}
