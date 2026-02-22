using Jaunty;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryKeyValuePairTests : IDisposable
{
    private readonly Database _db;

    public QueryKeyValuePairTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_KeyValuePair_IntInt_ReturnsPairs()
    {
        var results = _db.Connection.Query<KeyValuePair<int, int>>(
            "SELECT product_id, category_id FROM products LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.True(kvp.Value > 0);
        });
    }

    [Fact]
    public void Query_KeyValuePair_IntString_ReturnsPairs()
    {
        var results = _db.Connection.Query<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.NotNull(kvp.Value);
        });
    }

    [Fact]
    public void Query_KeyValuePair_StringString_ReturnsPairs()
    {
        var results = _db.Connection.Query<KeyValuePair<string, string>>(
            "SELECT customer_id, company_name FROM customers LIMIT 3");

        Assert.Equal(3, results.Count);
        Assert.All(results, kvp =>
        {
            Assert.NotNull(kvp.Key);
            Assert.NotNull(kvp.Value);
        });
    }

    [Fact]
    public void QueryFirst_KeyValuePair_ReturnsFirstPair()
    {
        var result = _db.Connection.QueryFirst<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products ORDER BY product_id");

        Assert.Equal(1, result.Key);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public void Query_KeyValuePair_WithParameters_Works()
    {
        var results = _db.Connection.Query<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, kvp =>
        {
            Assert.True(kvp.Key > 0);
            Assert.NotNull(kvp.Value);
        });
    }

    [Fact]
    public async Task QueryAsync_KeyValuePair_Works()
    {
        var results = await _db.Connection.QueryAsync<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void QueryStream_KeyValuePair_Streams()
    {
        var results = _db.Connection.QueryStream<KeyValuePair<int, string>>(
            "SELECT product_id, product_name FROM products LIMIT 5").ToList();

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_KeyValuePair_AggregateQuery()
    {
        // Get count per category
        var results = _db.Connection.Query<KeyValuePair<int, long>>(
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

    [Fact]
    public void Query_KeyValuePair_TooFewColumns_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.Query<KeyValuePair<int, string>>(
                "SELECT product_id FROM products LIMIT 1"));

        Assert.Contains("at least 2 columns", ex.Message);
    }

    [Fact]
    public void Query_KeyValuePair_HandlesNullValue()
    {
        // Get a supplier with NULL region
        var result = _db.Connection.QueryFirst<KeyValuePair<int, string?>>(
            @"SELECT supplier_id, region FROM suppliers WHERE region IS NULL LIMIT 1");

        Assert.True(result.Key > 0);
        Assert.Null(result.Value);
    }
}

