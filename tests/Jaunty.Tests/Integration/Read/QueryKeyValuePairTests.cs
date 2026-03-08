using Jaunty;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryKeyValuePairTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryKeyValuePairTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static string ProductsTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Products" : "products";

    private static string CustomersTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Customers" : "customers";

    private static string SuppliersTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Suppliers" : "suppliers";

    private static string ProductIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "ProductId" : "product_id";

    private static string CategoryIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CategoryId" : "category_id";

    private static string ProductNameColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "ProductName" : "product_name";

    private static string CustomerIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CustomerId" : "customer_id";

    private static string CompanyNameColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CompanyName" : "company_name";

    private static string SupplierIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "SupplierId" : "supplier_id";

    private static string RegionColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Region" : "region";

    private static string TopOrLimit(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? $"TOP ({count}) " : string.Empty;

    private static string LimitSuffix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? string.Empty : $" LIMIT {count}";

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_IntInt_ReturnsPairs(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<KeyValuePair<int, int>>(
            $"SELECT {TopOrLimit(dialect, 5)}{ProductIdColumn(dialect)}, {CategoryIdColumn(dialect)} FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 5)}");

        Assert.Equal(5, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.True(kvp.Value > 0);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_IntString_ReturnsPairs(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<KeyValuePair<int, string>>(
            $"SELECT {TopOrLimit(dialect, 3)}{ProductIdColumn(dialect)}, {ProductNameColumn(dialect)} FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 3)}");

        Assert.Equal(3, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.NotNull(kvp.Value);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_StringString_ReturnsPairs(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<KeyValuePair<string, string>>(
            $"SELECT {TopOrLimit(dialect, 3)}{CustomerIdColumn(dialect)}, {CompanyNameColumn(dialect)} FROM {CustomersTable(dialect)}{LimitSuffix(dialect, 3)}");

        Assert.Equal(3, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.NotNull(kvp.Key);
            Assert.NotNull(kvp.Value);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirst_KeyValuePair_ReturnsFirstPair(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<KeyValuePair<int, string>>(
            $"SELECT {ProductIdColumn(dialect)}, {ProductNameColumn(dialect)} FROM {ProductsTable(dialect)} ORDER BY {ProductIdColumn(dialect)}");

        Assert.Equal(1, result.Key);
        Assert.NotNull(result.Value);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_WithParameters_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<KeyValuePair<int, string>>(
            $"SELECT {ProductIdColumn(dialect)}, {ProductNameColumn(dialect)} FROM {ProductsTable(dialect)} WHERE {CategoryIdColumn(dialect)} = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.NotNull(kvp.Value);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_KeyValuePair_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = await connection.QueryAsync<KeyValuePair<int, string>>(
            $"SELECT {TopOrLimit(dialect, 3)}{ProductIdColumn(dialect)}, {ProductNameColumn(dialect)} FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 3)}");

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryStream_KeyValuePair_Streams(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.QueryStream<KeyValuePair<int, string>>(
            $"SELECT {TopOrLimit(dialect, 5)}{ProductIdColumn(dialect)}, {ProductNameColumn(dialect)} FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 5)}").ToList();

        Assert.Equal(5, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_AggregateQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        if (dialect.Provider == DialectProvider.SqlServer)
        {
            var results = connection.Query<KeyValuePair<int, int>>(
                $@"SELECT {CategoryIdColumn(dialect)}, COUNT(*) AS product_count
                  FROM {ProductsTable(dialect)}
                  GROUP BY {CategoryIdColumn(dialect)}
                  ORDER BY {CategoryIdColumn(dialect)}");

            Assert.NotEmpty(results);
            Assert.All(results, kvp =>
            {
                Assert.True(kvp.Key > 0);
                Assert.True(kvp.Value > 0);
            });
        }
        else
        {
            var results = connection.Query<KeyValuePair<int, long>>(
                $@"SELECT {CategoryIdColumn(dialect)}, COUNT(*) AS product_count
                  FROM {ProductsTable(dialect)}
                  GROUP BY {CategoryIdColumn(dialect)}
                  ORDER BY {CategoryIdColumn(dialect)}");

            Assert.NotEmpty(results);
            Assert.All(results, kvp =>
            {
                Assert.True(kvp.Key > 0);
                Assert.True(kvp.Value > 0);
            });
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_TooFewColumns_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.Query<KeyValuePair<int, string>>(
                $"SELECT {TopOrLimit(dialect, 1)}{ProductIdColumn(dialect)} FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 1)}"));

        Assert.Contains("at least 2 columns", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_KeyValuePair_HandlesNullValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Get a supplier with NULL region
        var result = connection.QueryFirst<KeyValuePair<int, string?>>(
            $@"SELECT {TopOrLimit(dialect, 1)}{SupplierIdColumn(dialect)}, {RegionColumn(dialect)}
               FROM {SuppliersTable(dialect)}
               WHERE {RegionColumn(dialect)} IS NULL{LimitSuffix(dialect, 1)}");

        Assert.True(result.Key > 0);
        Assert.Null(result.Value);
    }
}