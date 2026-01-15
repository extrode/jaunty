using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QuerySingleTests : IDisposable
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

    public QuerySingleTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QuerySingle_WithSingleResult_ReturnsResult()
    {
var product = _db.Connection.QuerySingle<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
        Assert.NotNull(product.ProductName);
    }

    [Fact]
    public void QuerySingle_WithParameters_FiltersCorrectly()
    {
var product = _db.Connection.QuerySingle<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id AND category_id = @CategoryId",
            new { Id = 1, CategoryId = 1 });

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
        Assert.Equal((short?)1, product.CategoryId);
    }

    [Fact]
    public void QuerySingle_NoResults_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QuerySingle<Product>(
                "SELECT product_id AS ProductId, product_name AS ProductName, unit_price AS UnitPrice FROM products WHERE product_id = @Id",
                new { Id = -999 }));

        Assert.Contains("Sequence contains no elements", ex.Message);
    }

    [Fact]
    public void QuerySingle_MultipleResults_Throws()
    {
var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QuerySingle<Product>(
                $"SELECT {FullProductColumns} FROM products WHERE category_id = @CategoryId",
                new { CategoryId = 1 }));

        Assert.Contains("Sequence contains more than one element", ex.Message);
    }

    [Fact]
    public void QuerySingle_WithCommandOptions_Works()
    {
var product = _db.Connection.QuerySingle<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 1 },
            CommandOptions<Product>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(1, product.ProductId);
    }

    [Fact]
    public void QuerySingle_StrictMapping_MissingColumn_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QuerySingle<Product>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @Id",
                new { Id = 1 }));

Assert.Contains("Strict mapping failed", ex.Message);
        // Check for any of the expected missing properties
        var missingProperties = new[] { "supplier_id", "category_id", "quantity_per_unit", "unit_price", "units_in_stock", "units_on_order", "reorder_level", "discontinued" };
        Assert.True(missingProperties.Any(prop => ex.Message.Contains(prop)), $"Expected one of {string.Join(", ", missingProperties)} in error message: {ex.Message}");
    }
}
