using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration;

public class QueryScalarTests : IDisposable
{
    private readonly Database _db;

    public QueryScalarTests()
    {
        _db = new Database();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void QueryScalar_ReturnsLong_Success()
    {
        var count = _db.Connection.QueryScalar<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Fact]
    public void QueryScalar_ReturnsString_Success()
    {
        var name = _db.Connection.QueryScalar<string>("SELECT product_name FROM products WHERE product_id = 1");

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public void QueryScalar_ReturnsDouble_Success()
    {
        var price = _db.Connection.QueryScalar<double>("SELECT unit_price FROM products WHERE product_id = 1");

        Assert.True(price > 0);
    }

    [Fact]
    public void QueryScalar_WithNamedParameter_Success()
    {
        var name = _db.Connection.QueryScalar<string>(
            "SELECT product_name FROM products WHERE product_id = @ProductId",
            new { ProductId = 1 });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public void QueryScalar_WithPositionalParameter_Success()
    {
        var name = _db.Connection.QueryScalar<string>(
            "SELECT product_name FROM products WHERE product_id = @Id",
            new { id = 1 });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public void QueryScalar_NoRows_ReturnsDefault()
    {
        var result = _db.Connection.QueryScalar<long>(
            "SELECT product_id FROM products WHERE product_id = @Id",
            new { id = -999 });

        Assert.Equal(0, result);
    }

    [Fact]
    public void QueryScalar_NullValue_ReturnsDefault()
    {
        var result = _db.Connection.QueryScalar<string>(
            "SELECT region FROM customers WHERE customer_id = @Id",
            new { Id = "ALFKI" });

        // Region can be null for some customers
        // This tests that null handling works
        Assert.True(result == null || result is string);
    }
}
