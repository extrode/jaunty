using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class ExecuteScalarTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ExecuteScalarTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_Count_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products")
            : connection.ExecuteScalar<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_WithParameters_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products WHERE CategoryId = @CategoryId", new { CategoryId = 1 })
            : connection.ExecuteScalar<long>("SELECT COUNT(*) FROM products WHERE category_id = @CategoryId", new { CategoryId = 1 });

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products", CommandOptions<int>.WithTimeout(30))
            : connection.ExecuteScalar<long>("SELECT COUNT(*) FROM products", CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = dialect.Provider == DialectProvider.SqlServer
            ? connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Products WHERE CategoryId = @CategoryId", new { CategoryId = 1 }, CommandOptions<int>.WithTimeout(30))
            : connection.ExecuteScalar<long>("SELECT COUNT(*) FROM products WHERE category_id = @CategoryId", new { CategoryId = 1 }, CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_StringValue_ReturnsString(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var name = connection.ExecuteScalar<string>(
            dialect.Provider == DialectProvider.SqlServer
                ? "SELECT TOP (1) ProductName FROM Products ORDER BY ProductId"
                : "SELECT product_name FROM products ORDER BY product_id LIMIT 1");

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_MaxValue_ReturnsCorrect(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var maxId = dialect.Provider == DialectProvider.SqlServer
            ? connection.ExecuteScalar<long>("SELECT MAX(ProductId) FROM Products")
            : connection.ExecuteScalar<long>("SELECT MAX(product_id) FROM products");

        Assert.True(maxId > 0);
    }
}