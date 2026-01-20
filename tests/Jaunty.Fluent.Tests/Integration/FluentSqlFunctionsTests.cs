using FluentAssertions;

using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentSqlFunctionsTests : IDisposable
{
    private readonly Database _db;

    public FluentSqlFunctionsTests() => _db = new Database();
    public void Dispose() => _db.Dispose();

    // ==========================================
    // Sql.Coalesce Tests
    // ==========================================

    [Fact]
    public void Coalesce_InWhere_FiltersResults()
    {
        // Coalesce treats null as 0, so we filter products where stock (or 0) > 10
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 10)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            (p.UnitsInStock ?? 0) > 10);
    }

    [Fact]
    public void Coalesce_ToSql_GeneratesCoalesceFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 10)
            .ToSql();

        // SQLite uses COALESCE
        sql.Should().Contain("COALESCE");
        sql.Should().Contain("units_in_stock");
    }

    [Fact]
    public void Coalesce_WithNullableDecimal_ToSql_GeneratesCorrectSql()
    {
        // Note: SQLite has issues with C# decimal type in COALESCE, so we use double for actual filtering
        // This test just verifies the SQL generation
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitPrice, 0m) > 10m)
            .ToSql();

        sql.Should().Contain("COALESCE");
        sql.Should().Contain("unit_price");
        sql.Should().Contain(">");
        sql.Should().Contain("WHERE");
    }

    [Fact]
    public void Coalesce_ThreeArgs_ToSql_GeneratesCorrectFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, p.UnitsOnOrder, (short)0) > 0)
            .ToSql();

        sql.Should().Contain("COALESCE");
        sql.Should().Contain("units_in_stock");
        sql.Should().Contain("units_on_order");
    }

    // ==========================================
    // Sql.IsNull Tests
    // ==========================================

    [Fact]
    public void IsNull_InWhere_FiltersResults()
    {
        // IsNull treats null as 0, so we filter products where stock (or 0) > 10
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitsInStock, (short)0) > 10)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            (p.UnitsInStock ?? 0) > 10);
    }

    [Fact]
    public void IsNull_ToSql_GeneratesIsNullFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitsInStock, (short)0) > 10)
            .ToSql();

        // SQLite uses IFNULL
        sql.Should().Contain("IFNULL");
        sql.Should().Contain("units_in_stock");
    }

    [Fact]
    public void IsNull_WithDecimal_FiltersResults()
    {
        // Products where price (or 0) is less than 10
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitPrice, 0m) < 10m)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            (p.UnitPrice ?? 0m) < 10m);
    }

    // ==========================================
    // Sql.NullIf Tests
    // ==========================================

    [Fact]
    public void NullIf_ToSql_GeneratesNullIfFunction()
    {
        var sql = _db.Connection.From<Product>()
            .Where(p => Sql.NullIf(p.UnitsInStock, (short?)0) != null)
            .ToSql();

        sql.Should().Contain("NULLIF");
        sql.Should().Contain("units_in_stock");
    }

    [Fact]
    public void NullIf_InWhere_FiltersZeroValues()
    {
        // NullIf returns NULL if the value equals the second arg
        // So NullIf(stock, 0) != null means stock is not 0 (could still be null though)
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.NullIf(p.UnitsInStock, (short?)0) != null)
            .Select();

        // Products where UnitsInStock is not 0 and not null
        products.Should().OnlyContain(p =>
            p.UnitsInStock != null && p.UnitsInStock != 0);
    }

    // ==========================================
    // Combined Sql Functions Tests
    // ==========================================

    [Fact]
    public void Coalesce_CombinedWithAnd_FiltersCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 5)
            .And(p => p.Discontinued == false)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p =>
            (p.UnitsInStock ?? 0) > 5 &&
            p.Discontinued == false);
    }

    [Fact]
    public void IsNull_CombinedWithOrderBy_WorksCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitPrice, 0m) > 0m)
            .OrderBy(p => p.UnitPrice)
            .Take(10)
            .Select();

        products.Should().NotBeEmpty();
        products.Should().BeInAscendingOrder(p => p.UnitPrice);
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task Coalesce_SelectAsync_FiltersCorrectly()
    {
        var products = await _db.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 10)
            .SelectAsync();

        products.Should().NotBeEmpty();
        products.Should().OnlyContain(p => (p.UnitsInStock ?? 0) > 10);
    }

    [Fact]
    public async Task IsNull_CountAsync_ReturnsFilteredCount()
    {
        var count = await _db.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitPrice, 0m) > 10m)
            .CountAsync();

        count.Should().BeGreaterThan(0);
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void Coalesce_WithConstantValue_WorksCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)100) >= 100)
            .Select();

        // This includes products where stock is null (coalesced to 100)
        // or where stock >= 100
        products.Should().NotBeEmpty();
    }

    [Fact]
    public void IsNull_WithConstantZero_HandlesNullsCorrectly()
    {
        var products = _db.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.ReorderLevel, (short)0) == 0)
            .Select();

        // Products where ReorderLevel is null OR ReorderLevel is 0
        products.Should().NotBeEmpty();
    }
}
