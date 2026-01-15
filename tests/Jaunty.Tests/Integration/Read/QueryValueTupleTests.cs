using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryValueTupleTests : IDisposable
{
    private readonly Database _db;

    public QueryValueTupleTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void Query_ValueTuple2_ReturnsTuples()
    {
        var results = _db.Connection.Query<(int ProductId, string ProductName)>(
            "SELECT product_id, product_name FROM products LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, t =>
        {
            Assert.True(t.ProductId > 0);
            Assert.NotNull(t.ProductName);
        });
    }

    [Fact]
    public void Query_ValueTuple3_ReturnsTuples()
    {
        var results = _db.Connection.Query<(int ProductId, string ProductName, int CategoryId)>(
            "SELECT product_id, product_name, category_id FROM products LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, t =>
        {
            Assert.True(t.ProductId > 0);
            Assert.NotNull(t.ProductName);
            Assert.True(t.CategoryId > 0);
        });
    }

    [Fact]
    public void Query_ValueTuple4_ReturnsTuples()
    {
        var results = _db.Connection.Query<(int ProductId, string ProductName, int CategoryId, decimal UnitPrice)>(
            "SELECT product_id, product_name, category_id, unit_price FROM products WHERE unit_price IS NOT NULL LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, t =>
        {
            Assert.True(t.ProductId > 0);
            Assert.NotNull(t.ProductName);
            Assert.True(t.CategoryId > 0);
            Assert.True(t.UnitPrice >= 0);
        });
    }

    [Fact]
    public void QueryFirst_ValueTuple_ReturnsFirstTuple()
    {
        var result = _db.Connection.QueryFirst<(int Id, string Name)>(
            "SELECT product_id, product_name FROM products ORDER BY product_id");

        Assert.Equal(1, result.Id);
        Assert.NotNull(result.Name);
    }

    [Fact]
    public void Query_ValueTuple_WithParameters_Works()
    {
        var results = _db.Connection.Query<(int Id, string Name)>(
            "SELECT product_id, product_name FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotEmpty(results);
        Assert.All(results, t =>
        {
            Assert.True(t.Id > 0);
            Assert.NotNull(t.Name);
        });
    }

    [Fact]
    public async Task QueryAsync_ValueTuple_Works()
    {
        var results = await _db.Connection.QueryAsync<(int Id, string Name)>(
            "SELECT product_id, product_name FROM products LIMIT 3");

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void QueryStream_ValueTuple_Streams()
    {
        var results = _db.Connection.QueryStream<(int Id, string Name)>(
            "SELECT product_id, product_name FROM products LIMIT 5").ToList();

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Query_ValueTuple_AggregateQuery()
    {
        var results = _db.Connection.Query<(int CategoryId, long Count)>(
            @"SELECT category_id, COUNT(*) AS product_count
              FROM products
              GROUP BY category_id
              ORDER BY category_id");

        Assert.NotEmpty(results);
        Assert.All(results, t =>
        {
            Assert.True(t.CategoryId > 0);
            Assert.True(t.Count > 0);
        });
    }

    [Fact]
    public void Query_ValueTuple_TooFewColumns_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.Query<(int Id, string Name, int Category)>(
                "SELECT product_id, product_name FROM products LIMIT 1"));

        Assert.Contains("requires 3 columns", ex.Message);
    }

    [Fact]
    public void Query_ValueTuple_JoinQuery()
    {
        var results = _db.Connection.Query<(int ProductId, string ProductName, string CategoryName)>(
            @"SELECT p.product_id, p.product_name, c.category_name
              FROM products p
              JOIN categories c ON p.category_id = c.category_id
              LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, t =>
        {
            Assert.True(t.ProductId > 0);
            Assert.NotNull(t.ProductName);
            Assert.NotNull(t.CategoryName);
        });
    }

    [Fact]
    public void Query_ValueTuple_MixedTypes()
    {
        var results = _db.Connection.Query<(string CustomerId, string CompanyName, string? Region)>(
            "SELECT customer_id, company_name, region FROM customers LIMIT 5");

        Assert.Equal(5, results.Count);
        Assert.All(results, t =>
        {
            Assert.NotNull(t.CustomerId);
            Assert.NotNull(t.CompanyName);
            // Region can be null
        });
    }

    [Fact]
    public void Query_ValueTuple5_ReturnsTuples()
    {
        var results = _db.Connection.Query<(int Id, string Name, int CategoryId, decimal Price, int Stock)>(
            @"SELECT product_id, product_name, category_id, unit_price, units_in_stock
              FROM products
              WHERE unit_price IS NOT NULL AND units_in_stock IS NOT NULL
              LIMIT 5");

        Assert.Equal(5, results.Count);
    }
}
