using Jaunty.Core;
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
    [MicrosoftSqlite]
    [SystemSqlite]
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
    [MicrosoftSqlite]
    [SystemSqlite]
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
    [MicrosoftSqlite]
    [SystemSqlite]
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
    [MicrosoftSqlite]
    [SystemSqlite]
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
    [MicrosoftSqlite]
    [SystemSqlite]
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
    [MicrosoftSqlite]
    [SystemSqlite]
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
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_NullValue_ReturnsDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var result = connection.QueryScalar<string>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT Region FROM Customers WHERE CustomerId = @Id"
                : "SELECT region FROM customers WHERE customer_id = @Id",
            new { Id = "ALFKI" });

        // ALFKI's Region is NULL in the seed data - verifies QueryScalar actually returns
        // null for a NULL database value rather than an empty string or throwing.
        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.QueryScalar<int>("SELECT COUNT(*) FROM Products", CommandOptions<int>.WithTimeout(30))
            : connection.QueryScalar<long>("SELECT COUNT(*) FROM products", CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryScalar_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.QueryScalar<int>("SELECT COUNT(*) FROM Products WHERE CategoryId = @CategoryId", new { CategoryId = 1 }, CommandOptions<int>.WithTimeout(30))
            : connection.QueryScalar<long>("SELECT COUNT(*) FROM products WHERE category_id = @CategoryId", new { CategoryId = 1 }, CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }
}