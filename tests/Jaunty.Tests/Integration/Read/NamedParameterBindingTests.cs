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

        // Northwind's seed data has non-discontinued products in category 1; a binding
        // regression that silently produced an always-false WHERE clause would return 0.
        Assert.True(count > 0);
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

        // A null @Region makes "@Region IS NULL" true, so the OR clause matches every
        // customer row; a binding regression that dropped the OR or bound Region as a real
        // DBNull-incompatible value would return 0 instead of the full customer count.
        Assert.True(count > 0);
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

        // Northwind's seed data spans 1996-1998, so orders after 1997-01-01 definitely exist.
        Assert.True(count > 0);
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

        // Northwind's seed data has many products priced above $10.
        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void NamedParameters_LikePattern_BindsCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.QueryScalar<int>(
                "SELECT COUNT(*) FROM Products WHERE ProductName LIKE @Name",
                new { Name = "%Chai%" })
            : connection.QueryScalar<long>(
                "SELECT COUNT(*) FROM products WHERE product_name LIKE @Name",
                new { Name = "%Chai%" });

        // Northwind's seed data has exactly one product named "Chai".
        Assert.True(count > 0);
    }
}