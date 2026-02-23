using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class NamedParameterBindingTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public NamedParameterBindingTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void NamedParameters_AnonymousObject_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.QueryScalar<int>(
                "SELECT COUNT(*) FROM Products WHERE CategoryId = @CategoryId AND Discontinued = @Discontinued",
                new { CategoryId = 1, Discontinued = false })
            : connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId AND discontinued = @Discontinued",
                new { CategoryId = 1, Discontinued = false });

        Assert.True(count >= 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void NamedParameters_NullValue_BindsAsDbNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        // This tests that null values are properly converted to DBNull
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.QueryScalar<int>(
                "SELECT COUNT(*) FROM Customers WHERE Region = @Region OR @Region IS NULL",
                new { Region = (string?)null })
            : connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM customers WHERE region = @Region OR @Region IS NULL",
                new { Region = (string?)null });

        Assert.True(count >= 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void NamedParameters_DateTime_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.QueryScalar<int>(
                "SELECT COUNT(*) FROM Orders WHERE OrderDate > @Date",
                new { Date = new DateTime(1997, 1, 1) })
            : connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM orders WHERE order_date > @Date",
                new { Date = new DateTime(1997, 1, 1) });

        Assert.True(count >= 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void NamedParameters_Decimal_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.QueryScalar<int>(
                "SELECT COUNT(*) FROM Products WHERE UnitPrice > @Price",
                new { Price = 10.0m })
            : connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE unit_price > @Price",
                new { Price = 10.0m });

        Assert.True(count >= 0);
    }
}



