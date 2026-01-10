using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Streaming;

public class QueryStreamAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryStreamAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public async Task QueryStreamAsync_WithResults_YieldsResults()
    {
        var products = _db.Connection.QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var count = 0;
        await foreach (var product in products)
        {
            Assert.True(product.ProductId > 0);
            Assert.NotNull(product.ProductName);
            count++;
            if (count >= 5) break; // Test that it streams incrementally
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task QueryStreamAsync_WithoutParameters_YieldsAll()
    {
        var products = _db.Connection.QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products LIMIT 3");

        var list = new List<Product>();
        await foreach (var product in products)
        {
            list.Add(product);
        }

        Assert.Equal(3, list.Count);
        Assert.All(list, p => Assert.True(p.ProductId > 0));
    }

    [Fact]
    public async Task QueryStreamAsync_WithCommandOptions_Works()
    {
        var products = _db.Connection.QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<Product>.WithTimeout(30));

        var count = 0;
        await foreach (var product in products)
        {
            Assert.True(product.ProductId > 0);
            count++;
            if (count >= 3) break;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task QueryStreamAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Reasonable timeout
        
        var products = _db.Connection.QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products LIMIT 5",
            cts.Token);

        var count = 0;
        await foreach (var product in products.WithCancellation(cts.Token))
        {
            Assert.True(product.ProductId > 0);
            count++;
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task QueryStreamAsync_PartialMapping_YieldsResults()
    {
        var summaries = _db.Connection.QueryStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 5");

        var list = new List<ProductSummary>();
        await foreach (var summary in summaries)
        {
            list.Add(summary);
        }

        Assert.Equal(5, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public async Task QueryStreamAsync_EmptyResult_YieldsNothing()
    {
        var products = _db.Connection.QueryStreamAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE product_id = @Id",
            new { Id = -999 });

        var list = new List<Product>();
        await foreach (var product in products)
        {
            list.Add(product);
        }

        Assert.Empty(list);
    }
}