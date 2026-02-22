#if NET8_0_OR_GREATER
using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Streaming;

public class QueryPartialStreamAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialStreamAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public async Task QueryPartialStreamAsync_WithResults_YieldsResults()
    {
        var summaries = _db.Connection.QueryPartialStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            Assert.True(summary.ProductId > 0);
            Assert.NotNull(summary.ProductName);
            count++;
            if (count >= 5) break; // Test that it streams incrementally
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task QueryPartialStreamAsync_WithoutParameters_YieldsAll()
    {
        var summaries = _db.Connection.QueryPartialStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 3");

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Equal(3, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public async Task QueryPartialStreamAsync_WithCommandOptions_Works()
    {
        var summaries = _db.Connection.QueryPartialStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            Assert.True(summary.ProductId > 0);
            count++;
            if (count >= 3) break;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task QueryPartialStreamAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Reasonable timeout
        
        var summaries = _db.Connection.QueryPartialStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 5",
            cts.Token);

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries.WithCancellation(cts.Token))
#else
        foreach (var summary in await summaries)
#endif
        {
            Assert.True(summary.ProductId > 0);
            count++;
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task QueryPartialStreamAsync_ExtraColumns_IgnoresExtra()
    {
        var summaries = _db.Connection.QueryPartialStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price, supplier_id FROM products LIMIT 5");

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Equal(5, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public async Task QueryPartialStreamAsync_MissingColumns_SetsDefaults()
    {
        var summaries = _db.Connection.QueryPartialStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products LIMIT 5");

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Equal(5, list.Count);
        Assert.All(list, s => 
        {
            Assert.True(s.ProductId > 0);
            Assert.Equal(string.Empty, s.ProductName); // Default value
        });
    }

    [Fact]
    public async Task QueryPartialStreamAsync_EmptyResult_YieldsNothing()
    {
        var summaries = _db.Connection.QueryPartialStreamAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @Id",
            new { Id = -999 });

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Empty(list);
    }
}
#endif

