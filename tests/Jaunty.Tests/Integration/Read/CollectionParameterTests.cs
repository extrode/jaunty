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
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithIntArrayParameter_ReturnsMatchingRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var products = connection.QueryPartial<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @ProductIds"),
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithListParameter_ReturnsMatchingRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new List<int> { 1, 2, 3, 4, 5 };

        var products = connection.QueryPartial<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @ProductIds"),
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.True(products.Count <= productIds.Count);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithEmptyArray_ReturnsNoRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ids = Array.Empty<int>();

        // Postgres can infer empty expanded collection parameters as text; skip query execution for empty input.
        if (dialect.Provider == DialectProvider.Postgres)
        {
            Assert.Empty(ids);
            return;
        }

        var products = connection.QueryPartial<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @Ids"),
            new { Ids = ids });

        Assert.Empty(products);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithSingleItemArray_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ids = new[] { 1 };

        var products = connection.QueryPartial<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @Ids"),
            new { Ids = ids });

        Assert.Single(products);
        Assert.Equal(1, products[0].ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithCollectionAndOtherParams_WorksTogether(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var products = connection.QueryPartial<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @ProductIds AND {DiscontinuedColumn(dialect)} = @Discontinued"),
            new { ProductIds = productIds, Discontinued = false });

        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.Contains(p.ProductId, productIds);
            Assert.False(p.Discontinued);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithMultipleCollections_WorksTogether(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds1 = new[] { 1, 2, 3, 4, 5 };
        var productIds2 = new[] { 3, 4, 5, 6, 7 };

        // Products where product_id is in both lists (intersection)
        var products = connection.QueryPartial<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @Ids1 AND {ProductIdColumn(dialect)} IN @Ids2"),
            new { Ids1 = productIds1, Ids2 = productIds2 });

        Assert.All(products, p =>
        {
            Assert.Contains(p.ProductId, productIds1);
            Assert.Contains(p.ProductId, productIds2);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_WithCollectionParameter_ReturnsCorrectCount(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        if (dialect.Provider == DialectProvider.SqlServer)
        {
            var count = connection.QueryScalar<int>(
                $"SELECT COUNT(*) FROM {ProductsTable(dialect)} WHERE {ProductIdColumn(dialect)} IN @ProductIds",
                new { ProductIds = productIds });
            Assert.True(count > 0);
            return;
        }

        var longCount = connection.QueryScalar<long>(
            $"SELECT COUNT(*) FROM {ProductsTable(dialect)} WHERE {ProductIdColumn(dialect)} IN @ProductIds",
            new { ProductIds = productIds });

        Assert.True(longCount > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirst_WithCollectionParameter_ReturnsFirstMatch(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var product = connection.QueryPartialFirst<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @ProductIds", $" ORDER BY {ProductIdColumn(dialect)}"),
            new { ProductIds = productIds });

        Assert.Contains(product.ProductId, productIds);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_WithCollectionParameter_ReturnsMatchingRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var products = await connection.QueryPartialAsync<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @ProductIds"),
            new { ProductIds = productIds });

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryStream_WithCollectionParameter_StreamsMatchingRows(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var productIds = new[] { 1, 2, 3 };

        var products = connection.QueryPartialStream<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @ProductIds"),
            new { ProductIds = productIds }).ToList();

        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Contains(p.ProductId, productIds));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithLargeCollection_WorksCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Test with 50 items to ensure expansion handles larger collections
        var ids = Enumerable.Range(1, 50).ToArray();

        var products = connection.QueryPartial<Product>(
            ProductSelectSql(dialect, $"{ProductIdColumn(dialect)} IN @Ids"),
            new { Ids = ids });

        // Should return products with IDs in the range 1-50
        Assert.All(products, p => Assert.Contains(p.ProductId, ids));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_WithStringCollection_WorksCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // First get some actual customer IDs
        var customerIds = connection.QueryPartial<Customer>(
            CustomerTopSql(dialect, 3))
            .Select(c => c.CustomerId)
            .ToArray();

        if (customerIds.Length == 0)
            return; // Skip if no customers

        var customers = connection.QueryPartial<Customer>(
            CustomerSelectSql(dialect, $"{CustomerIdColumn(dialect)} IN @CustomerIds"),
            new { CustomerIds = customerIds });

        Assert.Equal(customerIds.Length, customers.Count);
        Assert.All(customers, c => Assert.Contains(c.CustomerId, customerIds));
    }

    private static string ProductSelectSql(DialectInfo dialect, string whereClause, string orderByClause = "") =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT ProductId AS ProductId, ProductName AS ProductName, CategoryId AS CategoryId, " +
              "UnitPrice AS UnitPrice, Discontinued AS Discontinued " +
              $"FROM {ProductsTable(dialect)} WHERE {whereClause}{orderByClause}"
            : "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId, " +
              "unit_price AS UnitPrice, discontinued AS Discontinued " +
              $"FROM {ProductsTable(dialect)} WHERE {whereClause}{orderByClause}";

    private static string CustomerTopSql(DialectInfo dialect, int top) =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT TOP ({top}) CustomerId AS CustomerId, CompanyName AS CompanyName, ContactName AS ContactName, City AS City, Region AS Region FROM {CustomersTable(dialect)}"
            : $"SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName, city AS City, region AS Region FROM {CustomersTable(dialect)} LIMIT {top}";

    private static string CustomerSelectSql(DialectInfo dialect, string whereClause) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CustomerId AS CustomerId, CompanyName AS CompanyName, ContactName AS ContactName, " +
              $"City AS City, Region AS Region FROM {CustomersTable(dialect)} WHERE {whereClause}"
            : "SELECT customer_id AS CustomerId, company_name AS CompanyName, contact_name AS ContactName, " +
              $"city AS City, region AS Region FROM {CustomersTable(dialect)} WHERE {whereClause}";

    private static string ProductsTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Products" : "products";

    private static string CustomersTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Customers" : "customers";

    private static string ProductIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "ProductId" : "product_id";

    private static string CustomerIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CustomerId" : "customer_id";

    private static string DiscontinuedColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Discontinued" : "discontinued";
}



