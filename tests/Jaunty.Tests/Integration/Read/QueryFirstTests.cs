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
        supplier_id, 
        category_id, 
        quantity_per_unit, 
        unit_price, 
        units_in_stock, 
        units_on_order, 
        reorder_level, 
        discontinued";

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

        Assert.Contains("Sequence contains no elements", ex.Message);
    }

    [Fact]
    public void QueryFirst_WithCommandOptions_Works()
    {
var product = _db.Connection.QueryFirst<Product>(
            @"SELECT product_id AS ProductId, product_name AS ProductName, 
                     supplier_id, category_id, quantity_per_unit, unit_price, 
                     units_in_stock, units_on_order, reorder_level, discontinued 
              FROM products WHERE product_id = @Id",
            new { Id = 1 },
            CommandOptions<Product>.WithTimeout(30));

        Assert.Equal(1, product.ProductId);
    }

    [Fact]
    public void QueryFirst_StrictMapping_MissingColumn_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QueryFirst<Product>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @Id",
                new { Id = 1 }));

Assert.Contains("Strict mapping failed", ex.Message);
        // Check for any of the expected missing properties
        var missingProperties = new[] { "supplier_id", "category_id", "quantity_per_unit", "unit_price", "units_in_stock", "units_on_order", "reorder_level", "discontinued" };
        Assert.True(missingProperties.Any(prop => ex.Message.Contains(prop)), $"Expected one of {string.Join(", ", missingProperties)} in error message: {ex.Message}");
    }
}
