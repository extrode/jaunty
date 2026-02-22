using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class CollectionParameterTests : IDisposable
{
    private readonly Database _db;

    public CollectionParameterTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_WithIntArrayParameter_ReturnsMatchingRows()
    {
        var productIds = new[] { 1, 2, 3 };

        var products = _db.Connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Fact]
    public void Query_WithListParameter_ReturnsMatchingRows()
    {
        var productIds = new List<int> { 1, 2, 3, 4, 5 };

        var products = _db.Connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.True(products.Count <= productIds.Count);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Fact]
    public void Query_WithEmptyArray_ReturnsNoRows()
    {
        var ids = Array.Empty<int>();

        var products = _db.Connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @Ids",
            new { Ids = ids });

        Assert.Empty(products);
    }

    [Fact]
    public void Query_WithSingleItemArray_ReturnsSingleRow()
    {
        var ids = new[] { 1 };

        var products = _db.Connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @Ids",
            new { Ids = ids });

        Assert.Single(products);
        Assert.Equal(1, products[0].ProductId);
    }

    [Fact]
    public void Query_WithCollectionAndOtherParams_WorksTogether()
    {
        var productIds = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var products = _db.Connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds AND discontinued = @Discontinued",
            new { ProductIds = productIds, Discontinued = false });

        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.Contains(p.ProductId, productIds);
            Assert.False(p.Discontinued);
        });
    }

    [Fact]
    public void Query_WithMultipleCollections_WorksTogether()
    {
        var productIds1 = new[] { 1, 2, 3, 4, 5 };
        var productIds2 = new[] { 3, 4, 5, 6, 7 };

        // Products where product_id is in both lists (intersection)
        var products = _db.Connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @Ids1 AND product_id IN @Ids2",
            new { Ids1 = productIds1, Ids2 = productIds2 });

        Assert.All(products, p =>
        {
            Assert.Contains(p.ProductId, productIds1);
            Assert.Contains(p.ProductId, productIds2);
        });
    }

    [Fact]
    public void QueryScalar_WithCollectionParameter_ReturnsCorrectCount()
    {
        var productIds = new[] { 1, 2, 3 };

        var count = _db.Connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds });

        Assert.True(count > 0);
    }

    [Fact]
    public void QueryFirst_WithCollectionParameter_ReturnsFirstMatch()
    {
        var productIds = new[] { 1, 2, 3 };

        var product = _db.Connection.QueryPartialFirst<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds ORDER BY product_id",
            new { ProductIds = productIds });

        Assert.Contains(product.ProductId, productIds);
    }

    [Fact]
    public async Task QueryAsync_WithCollectionParameter_ReturnsMatchingRows()
    {
        var productIds = new[] { 1, 2, 3 };

        var products = await _db.Connection.QueryPartialAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Fact]
    public void QueryStream_WithCollectionParameter_StreamsMatchingRows()
    {
        var productIds = new[] { 1, 2, 3 };

        var products = _db.Connection.QueryPartialStream<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds }).ToList();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Fact]
    public void Query_WithLargeCollection_WorksCorrectly()
    {
        // Test with 50 items to ensure expansion handles larger collections
        var ids = Enumerable.Range(1, 50).ToArray();

        var products = _db.Connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @Ids",
            new { Ids = ids });

        // Should return products with IDs in the range 1-50
        Assert.All(products, p => Assert.Contains(p.ProductId, ids));
    }

    [Fact]
    public void Query_WithStringCollection_WorksCorrectly()
    {
        // First get some actual customer IDs
        var customerIds = _db.Connection.QueryPartial<Customer>(
            "SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName, " +
            "city AS City, region AS Region " +
            "FROM customers LIMIT 3")
            .Select(c => c.CustomerId)
            .ToArray();

        if (customerIds.Length == 0)
            return; // Skip if no customers

        var customers = _db.Connection.QueryPartial<Customer>(
            "SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName, " +
            "city AS City, region AS Region " +
            "FROM customers WHERE customer_id IN @CustomerIds",
            new { CustomerIds = customerIds });

        Assert.Equal(customerIds.Length, customers.Count);
        Assert.All(customers, c => Assert.Contains(c.CustomerId, customerIds));
    }
}

