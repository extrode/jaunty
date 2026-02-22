using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryFirstTests : IDisposable
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

    public QueryFirstTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryFirst_WithResults_ReturnsFirst()
    {
        var product = _db.Connection.QueryFirst<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 1 });

        Assert.Equal(1, product.ProductId);
        Assert.NotNull(product.ProductName);
    }

    [Fact]
    public void QueryFirst_WithParameters_FiltersCorrectly()
    {
        var product = _db.Connection.QueryFirst<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(product.ProductId > 0);
        Assert.Equal((short?)1, product.CategoryId);
    }

    [Fact]
    public void QueryFirst_NoResults_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QueryFirst<Product>(
                $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
                new { Id = -999 }));

        Assert.Contains("Sequence contains no elements of type 'Product'", ex.Message);
    }

    [Fact]
    public void QueryFirst_WithCommandOptions_Works()
    {
var product = _db.Connection.QueryFirst<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 1 },
            CommandOptions<Product>.WithTimeout(30));

        Assert.Equal(1, product.ProductId);
    }

    [Fact]
    public void QueryFirst_StrictMapping_MissingColumn_Throws()
    {
        // Product implements IMapped<Product>, so its ReadEntity mapper runs directly.
        // Missing columns cause GetOrdinal to throw IndexOutOfRangeException.
        Assert.ThrowsAny<Exception>(() =>
            _db.Connection.QueryFirst<Product>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @Id",
                new { Id = 1 }));
    }
}

