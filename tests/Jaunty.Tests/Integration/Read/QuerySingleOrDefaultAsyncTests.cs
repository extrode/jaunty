using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QuerySingleOrDefaultAsyncTests : IDisposable
{
    private readonly Database _db;

    public QuerySingleOrDefaultAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_WithSingleResult_ReturnsResult()
    {
        var product = await _db.Connection.QuerySingleOrDefaultAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE product_id = @Id",
            new { Id = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
        Assert.NotNull(product.ProductName);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_WithParameters_FiltersCorrectly()
    {
        var product = await _db.Connection.QuerySingleOrDefaultAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE product_id = @Id AND category_id = @CategoryId",
            new { Id = 1, CategoryId = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
        Assert.Equal((short?)1, product.CategoryId);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _db.Connection.QuerySingleOrDefaultAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_MultipleResults_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _db.Connection.QuerySingleOrDefaultAsync<Product>(
                "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE category_id = @CategoryId",
                new { CategoryId = 1 }));

        Assert.Contains("Sequence contains more than one element", ex.Message);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_WithCommandOptions_Works()
    {
        var product = await _db.Connection.QuerySingleOrDefaultAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE product_id = @Id",
            new { Id = 1 },
            CommandOptions<Product>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();
        
        var product = await _db.Connection.QuerySingleOrDefaultAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE product_id = @Id",
            new { Id = 1 },
            cts.Token);

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_StrictMapping_MissingColumn_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _db.Connection.QuerySingleOrDefaultAsync<Product>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @Id",
                new { Id = 1 }));

        Assert.Contains("Strict mapping failed", ex.Message);
        Assert.Contains("UnitPrice", ex.Message);
    }
}
