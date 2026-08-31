using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Streaming;

public class QueryStreamAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryStreamAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static string ProductsTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Products" : "products";

    private static string TopPrefix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? $"TOP ({count}) " : string.Empty;

    private static string LimitSuffix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? string.Empty : $" LIMIT {count}";

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryStreamAsync_WithResults_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var products = connection.QueryStreamAsync<Product>(
            $"SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM {ProductsTable(dialect)} WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var product in products)
#else
        foreach (var product in await products)
#endif
        {
            Assert.True(product.ProductId > 0);
            Assert.NotNull(product.ProductName);
            count++;
            if (count >= 5) break; // Test that it streams incrementally
        }

        Assert.Equal(5, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryStreamAsync_WithoutParameters_YieldsAll(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var products = connection.QueryStreamAsync<Product>(
            $"SELECT {TopPrefix(dialect, 3)}product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 3)}");

        var list = new List<Product>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var product in products)
#else
        foreach (var product in await products)
#endif
        {
            list.Add(product);
        }

        Assert.Equal(3, list.Count);
        Assert.All(list, p => Assert.True(p.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryStreamAsync_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var products = connection.QueryStreamAsync<Product>(
            $"SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM {ProductsTable(dialect)} WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<Product>.WithTimeout(30));

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var product in products)
#else
        foreach (var product in await products)
#endif
        {
            Assert.True(product.ProductId > 0);
            count++;
            if (count >= 3) break;
        }

        Assert.Equal(3, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryStreamAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Reasonable timeout

        var products = connection.QueryStreamAsync<Product>(
            $"SELECT {TopPrefix(dialect, 5)}product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 5)}",
            cts.Token);

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var product in products.WithCancellation(cts.Token))
#else
        foreach (var product in await products)
#endif
        {
            Assert.True(product.ProductId > 0);
            count++;
        }

        Assert.Equal(5, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryStreamAsync_PartialMapping_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = connection.QueryPartialStreamAsync<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 5)}product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 5)}");

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Equal(5, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryStreamAsync_EmptyResult_YieldsNothing(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var products = connection.QueryStreamAsync<Product>(
            $"SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM {ProductsTable(dialect)} WHERE product_id = @Id",
            new { Id = -999 });

        var list = new List<Product>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var product in products)
#else
        foreach (var product in await products)
#endif
        {
            list.Add(product);
        }

        Assert.Empty(list);
    }
}
