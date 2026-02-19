using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryPartialAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public async Task QueryPartialAsync_MissingColumn_Allowed()
    {
        var summaries = await _db.Connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.False(string.IsNullOrEmpty(s.ProductName)));
    }

    [Fact]
    public async Task QueryPartialAsync_WithParameter_FiltersCorrectly()
    {
        var summaries = await _db.Connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { Id = 1 });

        Assert.NotEmpty(summaries);
    }

    [Fact]
    public async Task QueryPartialAsync_WithOptions_ReturnsEntities()
    {
        var summaries = await _db.Connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    [Fact]
    public async Task QueryPartialAsync_WithParametersAndOptions_ReturnsFilteredEntities()
    {
        var summaries = await _db.Connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { Id = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }
}