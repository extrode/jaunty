using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QuerySingleOrDefaultAsyncTests : IDisposable
{
    private readonly Database _db;
    
    private const string FullProductColumns = @"
        product_id AS ProductId,
        product_name AS ProductName,
        supplier_id AS SupplierId,
        category_id AS CategoryId,
        quantity_per_unit AS QuantityPerUnit,
        unit_price AS UnitPrice,
        units_in_stock AS UnitsInStock,
        units_on_order AS UnitsOnOrder,
        reorder_level AS ReorderLevel,
        discontinued AS Discontinued";

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

        Assert.Contains("Sequence contains more than one element of type 'Product'", ex.Message);
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
        // Product implements IMapped<Product>, so its ReadEntity mapper runs directly.
        // Missing columns cause GetOrdinal to throw IndexOutOfRangeException.
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await _db.Connection.QuerySingleOrDefaultAsync<Product>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @Id",
                new { Id = 1 }));
    }
}

