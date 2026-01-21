using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Streaming;

public class QueryPartialStreamTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialStreamTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryPartialStream_WithResults_YieldsResults()
    {
        var summaries = _db.Connection.QueryPartialStream<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var count = 0;
        foreach (var summary in summaries)
        {
            Assert.True(summary.ProductId > 0);
            Assert.NotNull(summary.ProductName);
            count++;
            if (count >= 5) break; // Test that it streams incrementally
        }

        Assert.Equal(5, count);
    }

    [Fact]
    public void QueryPartialStream_WithoutParameters_YieldsAll()
    {
        var summaries = _db.Connection.QueryPartialStream<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 3");

        var list = summaries.ToList();
        Assert.Equal(3, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public void QueryPartialStream_WithCommandOptions_Works()
    {
        var summaries = _db.Connection.QueryPartialStream<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        var count = 0;
        foreach (var summary in summaries)
        {
            Assert.True(summary.ProductId > 0);
            count++;
            if (count >= 3) break;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void QueryPartialStream_ExtraColumns_IgnoresExtra()
    {
        var summaries = _db.Connection.QueryPartialStream<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, unit_price, supplier_id FROM products LIMIT 5");

        var list = summaries.ToList();
        Assert.Equal(5, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public void QueryPartialStream_MissingColumns_SetsDefaults()
    {
        var summaries = _db.Connection.QueryPartialStream<ProductSummary>(
            "SELECT product_id AS ProductId FROM products LIMIT 5");

        var list = summaries.ToList();
        Assert.Equal(5, list.Count);
        Assert.All(list, s => 
        {
            Assert.True(s.ProductId > 0);
            Assert.Equal(string.Empty, s.ProductName); // Default value
        });
    }

    [Fact]
    public void QueryPartialStream_EmptyResult_YieldsNothing()
    {
        var summaries = _db.Connection.QueryPartialStream<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = @Id",
            new { Id = -999 });

        var list = summaries.ToList();
        Assert.Empty(list);
    }
}
