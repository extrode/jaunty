using Jaunty;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryDynamicTests : IDisposable
{
    private readonly Database _db;

    public QueryDynamicTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_Dynamic_ReturnsExpandoObjects()
    {
        var results = _db.Connection.Query<dynamic>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
        Assert.All(results, row =>
        {
            // Dynamic access works
            Assert.NotNull(row.product_id);
            Assert.NotNull(row.product_name);
        });
    }

    [Fact]
    public void Query_Dynamic_AccessPropertiesDirectly()
    {
        dynamic result = _db.Connection.QueryFirst<dynamic>(
            "SELECT product_id, product_name, unit_price FROM products WHERE product_id = 1");

        // Properties accessible directly via dynamic
        Assert.Equal(1L, (long)result.product_id);
        Assert.IsType<string>(result.product_name);
    }

    [Fact]
    public void Query_Dynamic_HandlesNullValues()
    {
        dynamic result = _db.Connection.QueryFirst<dynamic>(
            "SELECT product_id, region FROM products p " +
            "LEFT JOIN suppliers s ON p.supplier_id = s.supplier_id " +
            "WHERE s.region IS NULL LIMIT 1");

        Assert.Null(result.region);
    }

    [Fact]
    public void QueryFirst_Dynamic_ReturnsFirstRow()
    {
        dynamic result = _db.Connection.QueryFirst<dynamic>(
            "SELECT product_id, product_name FROM products ORDER BY product_id");

        Assert.Equal(1L, (long)result.product_id);
    }

    [Fact]
    public void QueryFirstOrDefault_Dynamic_ReturnsNullWhenEmpty()
    {
        var result = _db.Connection.QueryFirstOrDefault<dynamic>(
            "SELECT product_id FROM products WHERE product_id = -999");

        Assert.Null(result);
    }

    [Fact]
    public void QuerySingle_Dynamic_ReturnsSingleRow()
    {
        dynamic result = _db.Connection.QuerySingle<dynamic>(
            "SELECT product_id, product_name FROM products WHERE product_id = 1");

        Assert.Equal(1L, (long)result.product_id);
    }

    [Fact]
    public void Query_Dynamic_WithParameters()
    {
        var results = _db.Connection.Query<dynamic>(
            "SELECT product_id, product_name FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, row =>
        {
            dynamic r = row;
            Assert.NotNull(r.product_name);
        });
    }

    [Fact]
    public async Task QueryAsync_Dynamic_Works()
    {
        var results = await _db.Connection.QueryAsync<dynamic>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void QueryStream_Dynamic_Streams()
    {
        var results = _db.Connection.QueryStream<dynamic>(
            "SELECT product_id, product_name FROM products LIMIT 5").ToList();

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_Dynamic_CountQuery()
    {
        dynamic result = _db.Connection.QueryFirst<dynamic>(
            "SELECT COUNT(*) AS total_count FROM products");

        Assert.True((long)result.total_count > 0);
    }

    [Fact]
    public void Query_Dynamic_AggregateQuery()
    {
        dynamic result = _db.Connection.QueryFirst<dynamic>(
            @"SELECT
                category_id,
                COUNT(*) AS product_count
              FROM products
              WHERE category_id = 1
              GROUP BY category_id");

        Assert.NotNull(result.category_id);
        Assert.True((long)result.product_count > 0);
    }

    [Fact]
    public void Query_Dynamic_CanIterateAsIDictionary()
    {
        dynamic result = _db.Connection.QueryFirst<dynamic>(
            "SELECT product_id, product_name FROM products WHERE product_id = 1");

        // ExpandoObject implements IDictionary<string, object>
        var dict = (IDictionary<string, object?>)result;
        Assert.True(dict.ContainsKey("product_id"));
        Assert.True(dict.ContainsKey("product_name"));
    }

    [Fact]
    public void Query_Dynamic_JoinQuery()
    {
        dynamic result = _db.Connection.QueryFirst<dynamic>(
            @"SELECT p.product_id, p.product_name, c.category_name
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              WHERE p.product_id = 1");

        Assert.NotNull(result.product_id);
        Assert.NotNull(result.product_name);
        Assert.NotNull(result.category_name);
    }
}

