using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Tests that IMapped&lt;T&gt;.ReadEntity is used as the custom mapper
/// when querying entities that implement the interface.
/// </summary>
public class IMappedTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    private static readonly string FullProductColumns =
        "product_id AS ProductId, product_name AS ProductName, " +
        "supplier_id AS SupplierId, category_id AS CategoryId, " +
        "quantity_per_unit AS QuantityPerUnit, unit_price AS UnitPrice, " +
        "units_in_stock AS UnitsInStock, units_on_order AS UnitsOnOrder, " +
        "reorder_level AS ReorderLevel, discontinued AS Discontinued";

    public IMappedTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }
[Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_IMappedEntity_UsesCustomMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var products = connection.Query<Product>(
            $"SELECT {FullProductColumns} FROM products LIMIT 3");

        Assert.Equal(3, products.Count);
        Assert.All(products, p =>
        {
            Assert.True(p.ProductId > 0);
            Assert.NotNull(p.ProductName);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirst_IMappedEntity_UsesCustomMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryFirst<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
        Assert.NotNull(product.ProductName);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirstOrDefault_IMappedEntity_UsesCustomMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryFirstOrDefault<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QuerySingle_IMappedEntity_UsesCustomMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QuerySingle<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
        Assert.NotNull(product.ProductName);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryStream_IMappedEntity_UsesCustomMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var products = connection.QueryStream<Product>(
            $"SELECT {FullProductColumns} FROM products LIMIT 5").ToList();

        Assert.Equal(5, products.Count);
        Assert.All(products, p =>
        {
            Assert.True(p.ProductId > 0);
            Assert.NotNull(p.ProductName);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_IMappedEntity_IgnoreAttributeWorks(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryFirst<Product>(
            $"SELECT {FullProductColumns} FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.Equal(product.ProductId, product.Id);
    }
}


