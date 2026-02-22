using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class CollectionParameterTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public CollectionParameterTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithIntArrayParameter_ReturnsMatchingRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var products = connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithListParameter_ReturnsMatchingRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new List<int> { 1, 2, 3, 4, 5 };

        var products = connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.True(products.Count <= productIds.Count);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithEmptyArray_ReturnsNoRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ids = Array.Empty<int>();

        var products = connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @Ids",
            new { Ids = ids });

        Assert.Empty(products);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithSingleItemArray_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ids = new[] { 1 };

        var products = connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @Ids",
            new { Ids = ids });

        Assert.Single(products);
        Assert.Equal(1, products[0].ProductId);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithCollectionAndOtherParams_WorksTogether(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var products = connection.QueryPartial<Product>(
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

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithMultipleCollections_WorksTogether(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds1 = new[] { 1, 2, 3, 4, 5 };
        var productIds2 = new[] { 3, 4, 5, 6, 7 };

        // Products where product_id is in both lists (intersection)
        var products = connection.QueryPartial<Product>(
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

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_WithCollectionParameter_ReturnsCorrectCount(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var count = connection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds });

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirst_WithCollectionParameter_ReturnsFirstMatch(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var product = connection.QueryPartialFirst<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds ORDER BY product_id",
            new { ProductIds = productIds });

        Assert.Contains(product.ProductId, productIds);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryAsync_WithCollectionParameter_ReturnsMatchingRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var products = await connection.QueryPartialAsync<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryStream_WithCollectionParameter_StreamsMatchingRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var products = connection.QueryPartialStream<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @ProductIds",
            new { ProductIds = productIds }).ToList();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithLargeCollection_WorksCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Test with 50 items to ensure expansion handles larger collections
        var ids = Enumerable.Range(1, 50).ToArray();

        var products = connection.QueryPartial<Product>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
            "unit_price AS UnitPrice, discontinued AS Discontinued " +
            "FROM products WHERE product_id IN @Ids",
            new { Ids = ids });

        // Should return products with IDs in the range 1-50
        Assert.All(products, p => Assert.Contains(p.ProductId, ids));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_WithStringCollection_WorksCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // First get some actual customer IDs
        var customerIds = connection.QueryPartial<Customer>(
            "SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName, " +
            "city AS City, region AS Region " +
            "FROM customers LIMIT 3")
            .Select(c => c.CustomerId)
            .ToArray();

        if (customerIds.Length == 0)
            return; // Skip if no customers

        var customers = connection.QueryPartial<Customer>(
            "SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName, " +
            "city AS City, region AS Region " +
            "FROM customers WHERE customer_id IN @CustomerIds",
            new { CustomerIds = customerIds });

        Assert.Equal(customerIds.Length, customers.Count);
        Assert.All(customers, c => Assert.Contains(c.CustomerId, customerIds));
    }
}



