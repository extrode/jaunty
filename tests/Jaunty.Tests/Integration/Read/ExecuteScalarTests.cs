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
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_Count_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.ExecuteScalar<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_WithParameters_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM products",
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var count = connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_StringValue_ReturnsString(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var name = connection.ExecuteScalar<string>(
            "SELECT product_name FROM products ORDER BY product_id LIMIT 1");

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ExecuteScalar_MaxValue_ReturnsCorrect(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var maxId = connection.ExecuteScalar<long>("SELECT MAX(product_id) FROM products");

        Assert.True(maxId > 0);
    }
}



