using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryScalarAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryScalarAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }
[Theory]
    [MicrosoftSqlite]
    public async Task QueryScalarAsync_ReturnsLong_Success(DialectInfo dialect)
    {
        var count = await _fixture.GetDbConnection(dialect).QueryScalarAsync<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryScalarAsync_ReturnsString_Success(DialectInfo dialect)
    {
        var name = await _fixture.GetDbConnection(dialect).QueryScalarAsync<string>("SELECT product_name FROM products WHERE product_id = 1");

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryScalarAsync_WithNamedParameter_Success(DialectInfo dialect)
    {
        var name = await _fixture.GetDbConnection(dialect).QueryScalarAsync<string>(
            "SELECT product_name FROM products WHERE product_id = @ProductId",
            new { ProductId = 1 });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryScalarAsync_NoRows_ReturnsDefault(DialectInfo dialect)
    {
        var result = await _fixture.GetDbConnection(dialect).QueryScalarAsync<long>(
            "SELECT product_id FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Equal(0, result);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryScalarAsync_WithCommandOptions_Success(DialectInfo dialect)
    {
        var count = await _fixture.GetDbConnection(dialect).QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM products",
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryScalarAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var cts = new CancellationTokenSource();

        var count = await _fixture.GetDbConnection(dialect).QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM products",
            cts.Token);

        Assert.True(count > 0);
    }
}



