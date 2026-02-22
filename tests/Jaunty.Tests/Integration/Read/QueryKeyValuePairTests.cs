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

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_KeyValuePair_IntInt_ReturnsPairs(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<KeyValuePair<int, int>>(
            "SELECT product_id, category_id FROM products LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.True(kvp.Value > 0);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_KeyValuePair_IntString_ReturnsPairs(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.NotNull(kvp.Value);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_KeyValuePair_StringString_ReturnsPairs(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<KeyValuePair<string, string>>(
            "SELECT customer_id, company_name FROM customers LIMIT 3");

        Assert.Equal(3, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.NotNull(kvp.Key);
            Assert.NotNull(kvp.Value);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryFirst_KeyValuePair_ReturnsFirstPair(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryFirst<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products ORDER BY product_id");

        Assert.Equal(1, result.Key);
        Assert.NotNull(result.Value);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_KeyValuePair_WithParameters_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.Query<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.NotNull(kvp.Value);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryAsync_KeyValuePair_Works()
    {
        var results = await connection.QueryAsync<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryStream_KeyValuePair_Streams(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var results = connection.QueryStream<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products LIMIT 5").ToList();

        Assert.Equal(5, results.Count);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_KeyValuePair_AggregateQuery(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Get count per category
        var results = connection.Query<KeyValuePair<int, long>>(
            @"SELECT category_id, COUNT(*) AS product_count
              FROM products
              GROUP BY category_id
              ORDER BY category_id");

        Assert.NotEmpty(results);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.True(kvp.Value > 0);
        });
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_KeyValuePair_TooFewColumns_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            connection.Query<KeyValuePair<int, string>>(
                "SELECT product_id FROM products LIMIT 1"));

        Assert.Contains("at least 2 columns", ex.Message);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Query_KeyValuePair_HandlesNullValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // Get a supplier with NULL region
        var result = connection.QueryFirst<KeyValuePair<int, string?>>(
            @"SELECT supplier_id, region FROM suppliers WHERE region IS NULL LIMIT 1");

        Assert.True(result.Key > 0);
        Assert.Null(result.Value);
    }
}



