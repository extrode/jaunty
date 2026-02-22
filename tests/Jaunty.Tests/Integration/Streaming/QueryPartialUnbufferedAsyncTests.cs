#if NET8_0_OR_GREATER
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Streaming;

public class QueryPartialUnbufferedAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialUnbufferedAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialUnbufferedAsync_WithResults_YieldsResults()
    {
        var count = 0;
        await foreach (var summary in _db.Connection.QueryPartialUnbufferedAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 }))
        {
            Assert.True(summary.ProductId > 0);
            count++;
            if (count >= 5) break;
        }

        Assert.Equal(5, count);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialUnbufferedAsync_WithoutParameters_YieldsAll()
    {
        var count = 0;
        await foreach (var summary in _db.Connection.QueryPartialUnbufferedAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 3"))
        {
            Assert.True(summary.ProductId > 0);
            count++;
        }

        Assert.Equal(3, count);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialUnbufferedAsync_WithCommandOptions_Works()
    {
        var count = 0;
        await foreach (var summary in _db.Connection.QueryPartialUnbufferedAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30)))
        {
            Assert.True(summary.ProductId > 0);
            count++;
            if (count >= 3) break;
        }

        Assert.Equal(3, count);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialUnbufferedAsync_WithOptionsOnly_Works()
    {
        var count = 0;
        await foreach (var summary in _db.Connection.QueryPartialUnbufferedAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 3",
            CommandOptions<ProductSummary>.WithTimeout(30)))
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialUnbufferedAsync_EmptyResult_YieldsNothing()
    {
        var count = 0;
        await foreach (var _ in _db.Connection.QueryPartialUnbufferedAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @Id",
            new { Id = -999 }))
        {
            count++;
        }

        Assert.Equal(0, count);
    }
}
#endif

