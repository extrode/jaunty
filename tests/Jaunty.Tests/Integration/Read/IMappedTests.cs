using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Tests that IMapped&lt;T&gt;.ReadEntity is used as the custom mapper
/// when querying entities that implement the interface.
/// </summary>
public class IMappedTests : IDisposable
{
    private readonly Database _db;

    private static readonly string FullProductColumns =
        "product_id AS ProductId, product_name AS ProductName, " +
        "supplier_id AS SupplierId, category_id AS CategoryId, " +
        "quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, " +
        "units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, " +
        "reorder_level AS ReorderLevel, discontinued AS Discontinued";

    public IMappedTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_IMappedEntity_UsesCustomMapper()
    {
        var products = _db.Connection.Query<Product>(
            $"SELECT {FullProductColumns} FROM products LIMIT 3");

        Assert.Equal(3, products.Count);
        Assert.All(products, p =>
        {
            Assert.True(p.ProductId > 0);
            Assert.NotNull(p.ProductName);
        });
    }

    [Fact]
    public void QueryFirst_IMappedEntity_UsesCustomMapper()
    {
        var product = _db.Connection.QueryFirst<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
        Assert.NotNull(product.ProductName);
    }

    [Fact]
    public void QueryFirstOrDefault_IMappedEntity_UsesCustomMapper()
    {
        var product = _db.Connection.QueryFirstOrDefault<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }

    [Fact]
    public void QuerySingle_IMappedEntity_UsesCustomMapper()
    {
        var product = _db.Connection.QuerySingle<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
        Assert.NotNull(product.ProductName);
    }

    [Fact]
    public void QueryStream_IMappedEntity_UsesCustomMapper()
    {
        var products = _db.Connection.QueryStream<Product>(
            $"SELECT {FullProductColumns} FROM products LIMIT 5").ToList();

        Assert.Equal(5, products.Count);
        Assert.All(products, p =>
        {
            Assert.True(p.ProductId > 0);
            Assert.NotNull(p.ProductName);
        });
    }

    [Fact]
    public void Query_IMappedEntity_IgnoreAttributeWorks()
    {
        var product = _db.Connection.QueryFirst<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.Equal(product.ProductId, product.Id);
    }
}

