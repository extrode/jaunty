using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class ExecuteScalarAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ExecuteScalarAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }
[Theory]
    [MicrosoftSqlite]
    public async Task ExecuteScalarAsync_Count_ReturnsValue(DialectInfo dialect)
    {
        var count = await _fixture.GetDbConnection(dialect).ExecuteScalarAsync<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ExecuteScalarAsync_WithParameters_ReturnsValue(DialectInfo dialect)
    {
        var count = await _fixture.GetDbConnection(dialect).ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ExecuteScalarAsync_WithOptionsOnly_Works(DialectInfo dialect)
    {
        var count = await _fixture.GetDbConnection(dialect).ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM products",
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task ExecuteScalarAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        var count = await _fixture.GetDbConnection(dialect).ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }
}



