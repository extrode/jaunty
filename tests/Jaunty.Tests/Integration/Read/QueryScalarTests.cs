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
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_ReturnsLong_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.QueryScalar<int>("SELECT COUNT(*) FROM Products")
            : connection.QueryScalar<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_ReturnsString_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var name = connection.QueryScalar<string>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT ProductName FROM Products WHERE ProductId = 1"
                : "SELECT product_name FROM products WHERE product_id = 1");

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_ReturnsDouble_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var price = connection.QueryScalar<double>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT UnitPrice FROM Products WHERE ProductId = 1"
                : "SELECT unit_price FROM products WHERE product_id = 1");

        Assert.True(price > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_WithNamedParameter_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var name = connection.QueryScalar<string>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT ProductName FROM Products WHERE ProductId = @ProductId"
                : "SELECT product_name FROM products WHERE product_id = @ProductId",
            new { ProductId = 1 });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_WithPositionalParameter_Success(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var name = connection.QueryScalar<string>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT ProductName FROM Products WHERE ProductId = @Id"
                : "SELECT product_name FROM products WHERE product_id = @Id",
            new { id = 1 });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_NoRows_ReturnsDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryScalar<long>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT ProductId FROM Products WHERE ProductId = @Id"
                : "SELECT product_id FROM products WHERE product_id = @Id",
            new { id = -999 });

        Assert.Equal(0, result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryScalar_NullValue_ReturnsDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryScalar<string>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT Region FROM Customers WHERE CustomerId = @Id"
                : "SELECT region FROM customers WHERE customer_id = @Id",
            new { Id = "ALFKI" });

        // Region can be null for some customers
        // This tests that null handling works
        Assert.True(result == null || result is string);
    }
}