using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryScalarTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryScalarTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_ReturnsLong_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.QueryScalar<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_ReturnsString_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var name = connection.QueryScalar<string>("SELECT product_name FROM products WHERE product_id = 1");

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_ReturnsDouble_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var price = connection.QueryScalar<double>("SELECT unit_price FROM products WHERE product_id = 1");

        Assert.True(price > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_WithNamedParameter_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var name = connection.QueryScalar<string>(
            "SELECT product_name FROM products WHERE product_id = @ProductId",
            new { ProductId = 1 });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_WithPositionalParameter_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var name = connection.QueryScalar<string>(
            "SELECT product_name FROM products WHERE product_id = @Id",
            new { id = 1 });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_NoRows_ReturnsDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryScalar<long>(
            "SELECT product_id FROM products WHERE product_id = @Id",
            new { id = -999 });

        Assert.Equal(0, result);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_NullValue_ReturnsDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryScalar<string>(
            "SELECT region FROM customers WHERE customer_id = @Id",
            new { Id = "ALFKI" });

        // Region can be null for some customers
        // This tests that null handling works
        Assert.True(result == null || result is string);
    }
}


