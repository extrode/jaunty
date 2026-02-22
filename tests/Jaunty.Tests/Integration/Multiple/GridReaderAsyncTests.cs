using Jaunty;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Multiple;

public class GridReaderAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public GridReaderAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }
[Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadAsync_ReturnsResults(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 2");

        var categories = (await gridReader.ReadAsync<Category>()).ToList();

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialAsync_AllowsMissingColumns(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 2");

        var categories = (await gridReader.ReadPartialAsync<CategorySummary>()).ToList(); // CategorySummary has fewer properties

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadFirstAsync_ReturnsFirst(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories ORDER BY category_id LIMIT 1");

        var category = await gridReader.ReadFirstAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadFirstOrDefaultAsync_ReturnsFirstOrNull(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories ORDER BY category_id LIMIT 1");

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadSingleAsync_ReturnsSingle(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var category = await gridReader.ReadSingleAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadSingleAsync_MultipleResults_Throws(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 2");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadSingleAsync<Category>());

        Assert.Contains("more than one element", ex.Message);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadSingleAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadSingleAsync<Category>());

        Assert.Contains("no elements", ex.Message);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadSingleOrDefaultAsync_ReturnsSingleOrDefault(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var category = await gridReader.ReadSingleOrDefaultAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadSingleOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var category = await gridReader.ReadSingleOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadScalarAsync_ReturnsValue(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories");

        var count = await gridReader.ReadScalarAsync<long>();

        Assert.True(count > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadScalarAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var cts = new CancellationTokenSource();
        
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories");

        var count = await gridReader.ReadScalarAsync<long>(cancellationToken: cts.Token);

        Assert.True(count > 0);
    }

#if NET8_0_OR_GREATER
    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadStreamAsync_YieldsResults(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 3");

        var categories = new List<Category>();
        await foreach (var category in gridReader.ReadStreamAsync<Category>())
        {
            categories.Add(category);
        }

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialStreamAsync_YieldsResults(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 3");

        var summaries = new List<CategorySummary>();
        await foreach (var summary in gridReader.ReadPartialStreamAsync<CategorySummary>())
        {
            summaries.Add(summary);
        }

        Assert.Equal(3, summaries.Count);
        Assert.All(summaries, s => Assert.NotNull(s.CategoryName));
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadStreamAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var cts = new CancellationTokenSource();

        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
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

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialFirstAsync_ReturnsFirst(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        var summary = await gridReader.ReadPartialFirstAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.True(summary.CategoryId > 0);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialFirstAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialFirstAsync<CategorySummary>());
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialFirstOrDefaultAsync_ReturnsFirst(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 3");

        var summary = await gridReader.ReadPartialFirstOrDefaultAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.True(summary.CategoryId > 0);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var summary = await gridReader.ReadPartialFirstOrDefaultAsync<CategorySummary>();

        Assert.Null(summary);
    }

    #endregion

    #region ReadPartialSingleAsync / ReadPartialSingleOrDefaultAsync

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialSingleAsync_ReturnsSingle(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var summary = await gridReader.ReadPartialSingleAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.CategoryId);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialSingleAsync_MultipleResults_Throws(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 2");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialSingleAsync<CategorySummary>());

        Assert.Contains("more than one element", ex.Message);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialSingleAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialSingleAsync<CategorySummary>());

        Assert.Contains("no elements", ex.Message);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialSingleOrDefaultAsync_ReturnsSingle(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = 1 });

        var summary = await gridReader.ReadPartialSingleOrDefaultAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.CategoryId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task GridReader_ReadPartialSingleOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var gridReader = await _fixture.GetDbConnection(dialect).QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id",
            new { Id = -999 });

        var summary = await gridReader.ReadPartialSingleOrDefaultAsync<CategorySummary>();

        Assert.Null(summary);
    }

    #endregion
}



