using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Multiple;

public class GridReaderAsyncTests : IDisposable
{
    private readonly Database _db;

    public GridReaderAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadAsync_ReturnsResults()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 2");

        var categories = (await gridReader.ReadAsync<Category>()).ToList();

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialAsync_AllowsMissingColumns()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 2");

        var categories = (await gridReader.ReadPartialAsync<CategorySummary>()).ToList(); // CategorySummary has fewer properties

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadFirstAsync_ReturnsFirst()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories ORDER BY category_id LIMIT 1");

        var category = await gridReader.ReadFirstAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
        Assert.NotNull(category.CategoryName);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadFirstOrDefaultAsync_ReturnsFirstOrNull()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories ORDER BY category_id LIMIT 1");

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
        Assert.NotNull(category.CategoryName);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadSingleAsync_ReturnsSingle()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var category = await gridReader.ReadSingleAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
        Assert.NotNull(category.CategoryName);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadSingleAsync_MultipleResults_Throws()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 2");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadSingleAsync<Category>());

        Assert.Contains("more than one element", ex.Message);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadSingleAsync_NoResults_Throws()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadSingleAsync<Category>());

        Assert.Contains("no elements", ex.Message);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadSingleOrDefaultAsync_ReturnsSingleOrDefault()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var category = await gridReader.ReadSingleOrDefaultAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadSingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var category = await gridReader.ReadSingleOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadScalarAsync_ReturnsValue()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories");

        var count = await gridReader.ReadScalarAsync<long>();

        Assert.True(count > 0);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadScalarAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();
        
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories");

        var count = await gridReader.ReadScalarAsync<long>(cancellationToken: cts.Token);

        Assert.True(count > 0);
    }

#if NET8_0_OR_GREATER
    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadStreamAsync_YieldsResults()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3");

        var categories = new List<Category>();
        await foreach (var category in gridReader.ReadStreamAsync<Category>())
        {
            categories.Add(category);
        }

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialStreamAsync_YieldsResults()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 3");

        var summaries = new List<CategorySummary>();
        await foreach (var summary in gridReader.ReadPartialStreamAsync<CategorySummary>())
        {
            summaries.Add(summary);
        }

        Assert.Equal(3, summaries.Count);
        Assert.All(summaries, s => Assert.NotNull(s.CategoryName));
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadStreamAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();

        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3");

        var categories = new List<Category>();
        await foreach (var category in gridReader.ReadStreamAsync<Category>(cancellationToken: cts.Token))
        {
            categories.Add(category);
        }

        Assert.Equal(3, categories.Count);
    }
#endif

    #region ReadPartialFirstAsync / ReadPartialFirstOrDefaultAsync

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialFirstAsync_ReturnsFirst()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        var summary = await gridReader.ReadPartialFirstAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.True(summary.CategoryId > 0);
        Assert.NotNull(summary.CategoryName);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialFirstAsync_NoResults_Throws()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialFirstAsync<CategorySummary>());
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialFirstOrDefaultAsync_ReturnsFirst()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        var summary = await gridReader.ReadPartialFirstOrDefaultAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.True(summary.CategoryId > 0);
        Assert.NotNull(summary.CategoryName);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var summary = await gridReader.ReadPartialFirstOrDefaultAsync<CategorySummary>();

        Assert.Null(summary);
    }

    #endregion

    #region ReadPartialSingleAsync / ReadPartialSingleOrDefaultAsync

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialSingleAsync_ReturnsSingle()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var summary = await gridReader.ReadPartialSingleAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.CategoryId);
        Assert.NotNull(summary.CategoryName);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialSingleAsync_MultipleResults_Throws()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 2");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialSingleAsync<CategorySummary>());

        Assert.Contains("more than one element", ex.Message);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialSingleAsync_NoResults_Throws()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialSingleAsync<CategorySummary>());

        Assert.Contains("no elements", ex.Message);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialSingleOrDefaultAsync_ReturnsSingle()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var summary = await gridReader.ReadPartialSingleOrDefaultAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.CategoryId);
    }

    [SkipSQLiteAsyncFact]
    public async Task GridReader_ReadPartialSingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var summary = await gridReader.ReadPartialSingleOrDefaultAsync<CategorySummary>();

        Assert.Null(summary);
    }

    #endregion
}

