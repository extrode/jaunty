using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

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

    [Fact]
    public async Task QueryAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();

        var categories = await _db.Connection.QueryAsync<Category>(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories",
            cts.Token);

        Assert.NotEmpty(categories);
    }
}