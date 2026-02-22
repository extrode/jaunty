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
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_ReturnsExpandoObjects(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<dynamic>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
        Assert.All(results, row =>
        {
            // Dynamic access works
            Assert.NotNull(row.product_id);
            Assert.NotNull(row.product_name);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_AccessPropertiesDirectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            "SELECT product_id, product_name, unit_price FROM products WHERE product_id = 1");

        // Properties accessible directly via dynamic
        Assert.Equal(1L, (long)result.product_id);
        Assert.IsType<string>(result.product_name);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_HandlesNullValues(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            "SELECT product_id, region FROM products p " +
            "LEFT JOIN suppliers s ON p.supplier_id = s.supplier_id " +
            "WHERE s.region IS NULL LIMIT 1");

        Assert.Null(result.region);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirst_Dynamic_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            "SELECT product_id, product_name FROM products ORDER BY product_id");

        Assert.Equal(1L, (long)result.product_id);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirstOrDefault_Dynamic_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirstOrDefault<dynamic>(
            "SELECT product_id FROM products WHERE product_id = -999");

        Assert.Null(result);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QuerySingle_Dynamic_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QuerySingle<dynamic>(
            "SELECT product_id, product_name FROM products WHERE product_id = 1");

        Assert.Equal(1L, (long)result.product_id);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_WithParameters(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<dynamic>(
            "SELECT product_id, product_name FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, row =>
        {
            dynamic r = row;
            Assert.NotNull(r.product_name);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryAsync_Dynamic_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = await connection.QueryAsync<dynamic>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryStream_Dynamic_Streams(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.QueryStream<dynamic>(
            "SELECT product_id, product_name FROM products LIMIT 5").ToList();

        Assert.Equal(5, results.Count);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_CountQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            "SELECT COUNT(*) AS total_count FROM products");

        Assert.True((long)result.total_count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_AggregateQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            @"SELECT
                category_id,
                COUNT(*) AS product_count
              FROM products
              WHERE category_id = 1
              GROUP BY category_id");

        Assert.NotNull(result.category_id);
        Assert.True((long)result.product_count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_CanIterateAsIDictionary(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            "SELECT product_id, product_name FROM products WHERE product_id = 1");

        // ExpandoObject implements IDictionary<string, object>
        var dict = (IDictionary<string, object?>)result;
        Assert.True(dict.ContainsKey("product_id"));
        Assert.True(dict.ContainsKey("product_name"));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_Dynamic_JoinQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        dynamic result = connection.QueryFirst<dynamic>(
            @"SELECT p.product_id, p.product_name, c.category_name
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE p.product_id = 1");

        Assert.NotNull(result.product_id);
        Assert.NotNull(result.product_name);
        Assert.NotNull(result.category_name);
    }
}



