using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

namespace Jaunty.Fluent.Tests.Integration;

public class FluentSqlFunctionsTests : IClassFixture<FluentDatabaseFixture>
{
    private readonly FluentDatabaseFixture _fixture;

    public FluentSqlFunctionsTests(FluentDatabaseFixture fixture) => _fixture = fixture;

    // ==========================================
    // Sql.Coalesce Tests
    // ==========================================

    [Fact]
    public void Coalesce_InWhere_FiltersResults()
    {
        // Coalesce treats null as 0, so we filter products where stock (or 0) > 10
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 10)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True((p.UnitsInStock ?? 0) > 10));
    }

    [Fact]
    public void Coalesce_ToSql_GeneratesCoalesceFunction()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 10)
            .ToSql();

        // SQLite uses COALESCE
        Assert.Contains("COALESCE", sql);
        Assert.Contains("units_in_stock", sql);
    }

    [Fact]
    public void Coalesce_WithNullableDecimal_ToSql_GeneratesCorrectSql()
    {
        // Note: SQLite has issues with C# decimal type in COALESCE, so we use double for actual filtering
        // This test just verifies the SQL generation
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitPrice, 0m) > 10m)
            .ToSql();

        Assert.Contains("COALESCE", sql);
        Assert.Contains("unit_price", sql);
        Assert.Contains(">", sql);
        Assert.Contains("WHERE", sql);
    }

    [Fact]
    public void Coalesce_ThreeArgs_ToSql_GeneratesCorrectFunction()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, p.UnitsOnOrder, (short)0) > 0)
            .ToSql();

        Assert.Contains("COALESCE", sql);
        Assert.Contains("units_in_stock", sql);
        Assert.Contains("units_on_order", sql);
    }

    // ==========================================
    // Sql.IsNull Tests
    // ==========================================

    [Fact]
    public void IsNull_InWhere_FiltersResults()
    {
        // IsNull treats null as 0, so we filter products where stock (or 0) > 10
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitsInStock, (short)0) > 10)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True((p.UnitsInStock ?? 0) > 10));
    }

    [Fact]
    public void IsNull_ToSql_GeneratesIsNullFunction()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitsInStock, (short)0) > 10)
            .ToSql();

        // SQLite uses IFNULL
        Assert.Contains("IFNULL", sql);
        Assert.Contains("units_in_stock", sql);
    }

    [Fact]
    public void IsNull_WithShort_FiltersResults()
    {
        // Products where reorder level (or 0) is less than 15
        // Note: SQLite doesn't handle C# decimal parameters correctly in IFNULL/COALESCE,
        // so we use short columns for actual query execution tests
        List<Product> products = _fixture.Connection.From<Product>()
                                               .Where(p => Sql.IsNull(p.ReorderLevel, (short)0) < (short)15)
                                               .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True((p.ReorderLevel ?? 0) < 15));
    }

    // ==========================================
    // Sql.NullIf Tests
    // ==========================================

    [Fact]
    public void NullIf_ToSql_GeneratesNullIfFunction()
    {
        var sql = _fixture.Connection.From<Product>()
            .Where(p => Sql.NullIf(p.UnitsInStock, (short?)0) != null)
            .ToSql();

        Assert.Contains("NULLIF", sql);
        Assert.Contains("units_in_stock", sql);
    }

    [Fact]
    public void NullIf_InWhere_FiltersZeroValues()
    {
        // NullIf returns NULL if the value equals the second arg
        // So NullIf(stock, 0) != null means stock is not 0 (could still be null though)
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.NullIf(p.UnitsInStock, (short?)0) != null)
            .Select();

        // Products where UnitsInStock is not 0 and not null
        Assert.All(products, p =>
            Assert.True(p.UnitsInStock != null && p.UnitsInStock != 0));
    }

    // ==========================================
    // Combined Sql Functions Tests
    // ==========================================

    [Fact]
    public void Coalesce_CombinedWithAnd_FiltersCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 5)
            .And(p => p.Discontinued == false)
            .Select();

        Assert.NotEmpty(products);
        Assert.All(products, p =>
            Assert.True((p.UnitsInStock ?? 0) > 5 &&
            p.Discontinued == false));
    }

    [Fact]
    public void IsNull_CombinedWithOrderBy_WorksCorrectly()
    {
        // Note: Using UnitsInStock (short) instead of UnitPrice (decimal)
        // because SQLite doesn't handle C# decimal parameters correctly in IFNULL
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitsInStock, (short)0) > (short)0)
            .OrderBy(p => p.UnitsInStock)
            .Take(10)
            .Select();

        Assert.NotEmpty(products);
        for (int i = 1; i < products.Count; i++)
        {
            Assert.True((products[i - 1].UnitsInStock ?? 0) <= (products[i].UnitsInStock ?? 0));
        }
    }

    // ==========================================
    // Async Tests
    // ==========================================

    [Fact]
    public async Task Coalesce_SelectAsync_FiltersCorrectly()
    {
        var products = await _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)0) > 10)
            .SelectAsync();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.True((p.UnitsInStock ?? 0) > 10));
    }

    [Fact]
    public async Task IsNull_CountAsync_ReturnsFilteredCount()
    {
        // Note: Using UnitsInStock (short) instead of UnitPrice (decimal)
        // because SQLite doesn't handle C# decimal parameters correctly in IFNULL
        var count = await _fixture.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.UnitsInStock, (short)0) > (short)10)
            .CountAsync();

        Assert.True(count > 0);
    }

    // ==========================================
    // Edge Cases
    // ==========================================

    [Fact]
    public void Coalesce_WithConstantValue_WorksCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.Coalesce(p.UnitsInStock, (short)100) >= 100)
            .Select();

        // This includes products where stock is null (coalesced to 100)
        // or where stock >= 100
        Assert.NotEmpty(products);
    }

    [Fact]
    public void IsNull_WithConstantZero_HandlesNullsCorrectly()
    {
        var products = _fixture.Connection.From<Product>()
            .Where(p => Sql.IsNull(p.ReorderLevel, (short)0) == 0)
            .Select();

        // Products where ReorderLevel is null OR ReorderLevel is 0
        Assert.NotEmpty(products);
    }
}
