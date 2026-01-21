using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Streaming;

public class QueryStreamTests : IDisposable
{
    private readonly Database _db;

    public QueryStreamTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryStream_WithResults_YieldsResults()
    {
        var products = _db.Connection.QueryStream<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var count = 0;
        foreach (var product in products)
        {
            Assert.True(product.ProductId > 0);
            Assert.NotNull(product.ProductName);
            count++;
            if (count >= 5) break; // Test that it streams incrementally
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public void QueryStream_WithoutParameters_YieldsAll()
    {
        var products = _db.Connection.QueryStream<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products LIMIT 3");

        var list = products.ToList();
        Assert.Equal(3, list.Count);
        Assert.All(list, p => Assert.True(p.ProductId > 0));
    }

    [Fact]
    public void QueryStream_WithCommandOptions_Works()
    {
        var products = _db.Connection.QueryStream<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<Product>.WithTimeout(30));

        var count = 0;
        foreach (var product in products)
        {
            Assert.True(product.ProductId > 0);
            count++;
            if (count >= 3) break;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void QueryStream_PartialMapping_YieldsResults()
    {
        var summaries = _db.Connection.QueryStream<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 5");

        var list = summaries.ToList();
        Assert.Equal(5, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public void QueryStream_EmptyResult_YieldsNothing()
    {
        var products = _db.Connection.QueryStream<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, supplier_id AS SupplierId, category_id AS CategoryId, quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, reorder_level AS ReorderLevel, discontinued AS Discontinued FROM products WHERE product_id = @Id",
            new { Id = -999 });

        var list = products.ToList();
        Assert.Empty(list);
    }
}
