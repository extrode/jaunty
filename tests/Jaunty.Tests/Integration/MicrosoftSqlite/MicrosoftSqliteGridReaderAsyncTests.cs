#if NET8_0_OR_GREATER
using Microsoft.Data.Sqlite;

using Jaunty;
using Jaunty.Tests.Entities;

namespace Jaunty.Tests.Integration.MicrosoftSqlite;

/// <summary>
/// Tests GridReader async operations using Microsoft.Data.Sqlite, which has proper async support
/// (unlike System.Data.SQLite which has limitations with async DataReader).
/// This covers ReadAsync, ReadScalarAsync, ReadStreamAsync, and DisposeAsync paths.
/// </summary>
public class MicrosoftSqliteGridReaderAsyncTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MicrosoftSqliteGridReaderAsyncTests()
    {
        _connection = new SqliteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _connection.Dispose();
    }

    [Fact]
    public async Task ReadAsync_ReturnsResults()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3");

        var categories = (await gridReader.ReadAsync<Category>()).ToList();

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Fact]
    public async Task ReadPartialAsync_ReturnsResults()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 3");

        var categories = (await gridReader.ReadPartialAsync<Category>()).ToList();

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Fact]
    public async Task ReadFirstAsync_ReturnsFirst()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories ORDER BY category_id LIMIT 3");

        var category = await gridReader.ReadFirstAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
    }

    [Fact]
    public async Task ReadFirstOrDefaultAsync_ReturnsFirst()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories ORDER BY category_id LIMIT 3");

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
    }

    [Fact]
    public async Task ReadFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [Fact]
    public async Task ReadSingleAsync_ReturnsSingle()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var category = await gridReader.ReadSingleAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Fact]
    public async Task ReadSingleAsync_MultipleResults_Throws()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 2");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadSingleAsync<Category>());

        Assert.Contains("more than one element", ex.Message);
    }

    [Fact]
    public async Task ReadSingleAsync_NoResults_Throws()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadSingleAsync<Category>());

        Assert.Contains("no elements", ex.Message);
    }

    [Fact]
    public async Task ReadSingleOrDefaultAsync_ReturnsSingle()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var category = await gridReader.ReadSingleOrDefaultAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Fact]
    public async Task ReadSingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var category = await gridReader.ReadSingleOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [Fact]
    public async Task ReadPartialFirstAsync_ReturnsFirst()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        var category = await gridReader.ReadPartialFirstAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
    }

    [Fact]
    public async Task ReadPartialFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var category = await gridReader.ReadPartialFirstOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [Fact]
    public async Task ReadPartialSingleAsync_ReturnsSingle()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var category = await gridReader.ReadPartialSingleAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Fact]
    public async Task ReadPartialSingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var category = await gridReader.ReadPartialSingleOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [Fact]
    public async Task ReadScalarAsync_ReturnsValue()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories");

        var count = await gridReader.ReadScalarAsync<long>();

        Assert.True(count > 0);
    }

    [Fact]
    public async Task ReadScalarAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();

        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories");

        var count = await gridReader.ReadScalarAsync<long>(cancellationToken: cts.Token);

        Assert.True(count > 0);
    }

    [Fact]
    public async Task ReadStreamAsync_YieldsResults()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3");

        var categories = new List<Category>();
        await foreach (var category in gridReader.ReadStreamAsync<Category>())
        {
            categories.Add(category);
        }

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Fact]
    public async Task ReadPartialStreamAsync_YieldsResults()
    {
        using var gridReader = await _connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 3");

        var categories = new List<Category>();
        await foreach (var category in gridReader.ReadPartialStreamAsync<Category>())
        {
            categories.Add(category);
        }

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Fact]
    public async Task DisposeAsync_ClosesReaderAndConnection()
    {
        var gridReader = await _connection.QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories");

        // Consume the result set
        await gridReader.ReadScalarAsync<long>();

        // DisposeAsync should dispose cleanly
        await gridReader.DisposeAsync();

        // No exception = success
    }
}
#endif
