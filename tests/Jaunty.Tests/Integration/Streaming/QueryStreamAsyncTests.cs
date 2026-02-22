#if NET8_0_OR_GREATER
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
[Theory]
    [MicrosoftSqlite]
    public async Task QueryStreamAsync_WithResults_YieldsResults(DialectInfo dialect)
    {
        var products = _fixture.GetDbConnection(dialect).QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products WHERE category_id = @CategoryId",
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
    [MicrosoftSqlite]
    public async Task QueryStreamAsync_WithoutParameters_YieldsAll(DialectInfo dialect)
    {
        var products = _fixture.GetDbConnection(dialect).QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products LIMIT 3");

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
    [MicrosoftSqlite]
    public async Task QueryStreamAsync_WithCommandOptions_Works(DialectInfo dialect)
    {
        var products = _fixture.GetDbConnection(dialect).QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products WHERE category_id = @CategoryId",
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
    [MicrosoftSqlite]
    public async Task QueryStreamAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Reasonable timeout
        
        var products = _fixture.GetDbConnection(dialect).QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products LIMIT 5",
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
    [MicrosoftSqlite]
    public async Task QueryStreamAsync_PartialMapping_YieldsResults(DialectInfo dialect)
    {
        var summaries = _fixture.GetDbConnection(dialect).QueryPartialStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 5");

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
    [MicrosoftSqlite]
    public async Task QueryStreamAsync_EmptyResult_YieldsNothing(DialectInfo dialect)
    {
        var products = _fixture.GetDbConnection(dialect).QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products WHERE product_id = @Id",
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
#endif



