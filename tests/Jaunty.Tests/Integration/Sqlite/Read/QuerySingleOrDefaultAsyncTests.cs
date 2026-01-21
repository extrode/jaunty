using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QuerySingleOrDefaultAsyncTests : IDisposable
{
    private readonly Database _db;
    
    private const string FullProductColumns = @"
        product_id AS ProductId, 
        product_name AS ProductName, 
        supplier_id, 
        category_id, 
        quantity_per_unit, 
        unit_price, 
        units_in_stock, 
        units_on_order, 
        reorder_level, 
        discontinued";

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
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
        Assert.NotNull(product.ProductName);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_WithParameters_FiltersCorrectly()
    {
        var product = await _db.Connection.QuerySingleOrDefaultAsync<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id AND category_id = @CategoryId",
            new { Id = 1, CategoryId = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
        Assert.Equal((short?)1, product.CategoryId);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _db.Connection.QuerySingleOrDefaultAsync<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_MultipleResults_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _db.Connection.QuerySingleOrDefaultAsync<Product>(
                $"SELECT {FullProductColumns} FROM products WHERE category_id = @CategoryId",
                new { CategoryId = 1 }));

        Assert.Contains("Sequence contains more than one element", ex.Message);
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_WithCommandOptions_Works()
    {
        var product = await _db.Connection.QuerySingleOrDefaultAsync<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
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
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
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
        // Check for any of the expected missing properties
        var missingProperties = new[] { "supplier_id", "category_id", "quantity_per_unit", "unit_price", "units_in_stock", "units_on_order", "reorder_level", "discontinued" };
        Assert.True(missingProperties.Any(prop => ex.Message.Contains(prop)), $"Expected one of {string.Join(", ", missingProperties)} in error message: {ex.Message}");
    }
}
