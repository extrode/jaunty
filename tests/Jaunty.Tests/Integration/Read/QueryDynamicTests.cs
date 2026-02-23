using Jaunty;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryDynamicTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryDynamicTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_ReturnsExpandoObjects(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<dynamic>(
            ProductTopSql(dialect, "product_id, product_name", 3));

        Assert.Equal(3, results.Count);
        Assert.All(results, row =>
        {
            // Dynamic access works
            Assert.NotNull(row.product_id);
            Assert.NotNull(row.product_name);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_AccessPropertiesDirectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            ProductWhereSql(dialect, "product_id, product_name, unit_price", $"{ProductIdColumn(dialect)} = 1"));

        // Properties accessible directly via dynamic
        Assert.Equal(1L, (long)result.product_id);
        Assert.IsType<string>(result.product_name);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_HandlesNullValues(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            ProductSupplierNullRegionSql(dialect));

        Assert.Null(result.region);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirst_Dynamic_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            ProductOrderBySql(dialect, "product_id, product_name"));

        Assert.Equal(1L, (long)result.product_id);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirstOrDefault_Dynamic_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirstOrDefault<dynamic>(
            ProductWhereSql(dialect, "product_id", $"{ProductIdColumn(dialect)} = -999"));

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QuerySingle_Dynamic_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QuerySingle<dynamic>(
            ProductWhereSql(dialect, "product_id, product_name", $"{ProductIdColumn(dialect)} = 1"));

        Assert.Equal(1L, (long)result.product_id);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_WithParameters(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<dynamic>(
            ProductWhereSql(dialect, "product_id, product_name", $"{CategoryIdColumn(dialect)} = @CategoryId"),
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, row =>
        {
            dynamic r = row;
            Assert.NotNull(r.product_name);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_Dynamic_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = await connection.QueryAsync<dynamic>(
            ProductTopSql(dialect, "product_id, product_name", 3));

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryStream_Dynamic_Streams(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.QueryStream<dynamic>(
            ProductTopSql(dialect, "product_id, product_name", 5)).ToList();

        Assert.Equal(5, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_CountQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            $"SELECT COUNT(*) AS total_count FROM {ProductsTable(dialect)}");

        Assert.True((long)result.total_count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_AggregateQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            @"SELECT
                category_id,
                COUNT(*) AS product_count
              FROM " + ProductsTable(dialect) + @"
              WHERE " + CategoryIdColumn(dialect) + @" = 1
              GROUP BY category_id");

        Assert.NotNull(result.category_id);
        Assert.True((long)result.product_count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_CanIterateAsIDictionary(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            ProductWhereSql(dialect, "product_id, product_name", $"{ProductIdColumn(dialect)} = 1"));

        // ExpandoObject implements IDictionary<string, object>
        var dict = (IDictionary<string, object?>)result;
        Assert.True(dict.ContainsKey("product_id"));
        Assert.True(dict.ContainsKey("product_name"));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_Dynamic_JoinQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            @"SELECT p.product_id, p.product_name, c.category_name
              FROM " + ProductsTable(dialect) + @" p
              JOIN " + CategoriesTable(dialect) + @" c ON p." + CategoryIdColumn(dialect) + @" = c." + CategoryIdColumn(dialect) + @"
              WHERE p." + ProductIdColumn(dialect) + @" = 1");

        Assert.NotNull(result.product_id);
        Assert.NotNull(result.product_name);
        Assert.NotNull(result.category_name);
    }

    private static string ProductTopSql(DialectInfo dialect, string aliasedColumns, int top) =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT TOP ({top}) {aliasedColumns} FROM {ProductsTable(dialect)}"
            : $"SELECT {aliasedColumns} FROM {ProductsTable(dialect)} LIMIT {top}";

    private static string ProductOrderBySql(DialectInfo dialect, string aliasedColumns) =>
        $"SELECT {aliasedColumns} FROM {ProductsTable(dialect)} ORDER BY {ProductIdColumn(dialect)}";

    private static string ProductWhereSql(DialectInfo dialect, string aliasedColumns, string whereClause) =>
        $"SELECT {aliasedColumns} FROM {ProductsTable(dialect)} WHERE {whereClause}";

    private static string ProductSupplierNullRegionSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP (1) product_id, region FROM Products p LEFT JOIN Suppliers s ON p.SupplierId = s.SupplierId WHERE s.Region IS NULL"
            : "SELECT product_id, region FROM products p LEFT JOIN suppliers s ON p.supplier_id = s.supplier_id WHERE s.region IS NULL LIMIT 1";

    private static string ProductsTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Products" : "products";

    private static string CategoriesTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Categories" : "categories";

    private static string ProductIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "ProductId" : "product_id";

    private static string CategoryIdColumn(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "CategoryId" : "category_id";
}



