using Jaunty.Core;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryScalarAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryScalarAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public async Task QueryScalarAsync_ReturnsLong_Success()
    {
        var count = await _db.Connection.QueryScalarAsync<long>("SELECT COUNT(*) FROM products");

        Assert.True(count > 0);
    }

    [Fact]
    public async Task QueryScalarAsync_ReturnsString_Success()
    {
        var name = await _db.Connection.QueryScalarAsync<string>("SELECT product_name FROM products WHERE product_id = 1");

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public async Task QueryScalarAsync_WithNamedParameter_Success()
    {
        var name = await _db.Connection.QueryScalarAsync<string>(
            "SELECT product_name FROM products WHERE product_id = @ProductId",
            new { ProductId = 1 });

        Assert.False(string.IsNullOrEmpty(name));
    }

    [Fact]
    public async Task QueryScalarAsync_NoRows_ReturnsDefault()
    {
        var result = await _db.Connection.QueryScalarAsync<long>(
            "SELECT product_id FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task QueryScalarAsync_WithCommandOptions_Success()
    {
        var count = await _db.Connection.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM products",
            CommandOptions<long>.WithTimeout(30));

        Assert.True(count > 0);
    }

    [Fact]
    public async Task QueryScalarAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();

        var count = await _db.Connection.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM products",
            cts.Token);

        Assert.True(count > 0);
    }
}
