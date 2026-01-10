using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration;

public class AsyncTests : IDisposable
{
    private readonly Database _db;

    public AsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    #region QueryScalarAsync

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

    #endregion

    #region QueryAsync

    [Fact]
    public async Task QueryAsync_ReturnsEntities()
    {
        var categories = await _db.Connection.QueryAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories");

        Assert.NotEmpty(categories);
        Assert.All(categories, c => Assert.False(string.IsNullOrEmpty(c.CategoryName)));
    }

    [Fact]
    public async Task QueryAsync_WithNamedParameter_FiltersCorrectly()
    {
        var categories = await _db.Connection.QueryAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.Single(categories);
        Assert.Equal(1, categories[0].CategoryId);
    }

    [Fact]
    public async Task QueryAsync_NoRows_ReturnsEmptyList()
    {
        var categories = await _db.Connection.QueryAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id", new { Id = -999 });

        Assert.Empty(categories);
    }

    [Fact]
    public async Task QueryAsync_StrictMode_MissingColumn_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _db.Connection.QueryAsync<Category>(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories"));

        Assert.Contains("Description", ex.Message);
        Assert.Contains("Strict mapping failed", ex.Message);
    }

    #endregion

    #region QueryPartialAsync

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

    #endregion

    #region Cancellation

    [Fact]
    public async Task QueryScalarAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();
        
        var count = await _db.Connection.QueryScalarAsync<long>(
            "SELECT COUNT(*) FROM products",
            cts.Token);

        Assert.True(count > 0);
    }

    [Fact]
    public async Task QueryAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();
        
        var categories = await _db.Connection.QueryAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            cts.Token);

        Assert.NotEmpty(categories);
    }

    #endregion
}
