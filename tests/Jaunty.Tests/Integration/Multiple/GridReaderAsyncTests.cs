using Jaunty.Core;
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
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadAsync_ReturnsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategorySql(dialect, 2));

        var categories = (await gridReader.ReadAsync<Category>()).ToList();

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialAsync_AllowsMissingColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategorySql(dialect, 2));

        var categories = (await gridReader.ReadPartialAsync<CategorySummary>()).ToList(); // CategorySummary has fewer properties

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadFirstAsync_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategorySql(dialect, 1, orderById: true));

        var category = await gridReader.ReadFirstAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadFirstOrDefaultAsync_ReturnsFirstOrNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategorySql(dialect, 1, orderById: true));

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategoryByIdSql(dialect),
            new { Id = -999 });

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadSingleAsync_ReturnsSingle(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategoryByIdSql(dialect),
            new { Id = 1 });

        var category = await gridReader.ReadSingleAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadSingleAsync_MultipleResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategorySql(dialect, 2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadSingleAsync<Category>());

        Assert.Contains("more than one element", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadSingleAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategoryByIdSql(dialect),
            new { Id = -999 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadSingleAsync<Category>());

        Assert.Contains("no elements", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadSingleOrDefaultAsync_ReturnsSingleOrDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategoryByIdSql(dialect),
            new { Id = 1 });

        var category = await gridReader.ReadSingleOrDefaultAsync<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadSingleOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategoryByIdSql(dialect),
            new { Id = -999 });

        var category = await gridReader.ReadSingleOrDefaultAsync<Category>();

        Assert.Null(category);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadScalarAsync_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            CountCategoriesSql(dialect));

        var count = await gridReader.ReadScalarAsync<long>();

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadScalarAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();

        using var gridReader = await connection.QueryMultipleAsync(
            CountCategoriesSql(dialect));

        var count = await gridReader.ReadScalarAsync<long>(cancellationToken: cts.Token);

        Assert.True(count > 0);
    }

#if NET8_0_OR_GREATER
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadStreamAsync_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            FullCategorySql(dialect, 3));

        var categories = new List<Category>();
        await foreach (var category in gridReader.ReadStreamAsync<Category>())
        {
            categories.Add(category);
        }

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialStreamAsync_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategorySql(dialect, 3));

        var summaries = new List<CategorySummary>();
        await foreach (var summary in gridReader.ReadPartialStreamAsync<CategorySummary>())
        {
            summaries.Add(summary);
        }

        Assert.Equal(3, summaries.Count);
        Assert.All(summaries, s => Assert.NotNull(s.CategoryName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadStreamAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();

        using var gridReader = await connection.QueryMultipleAsync(
            FullCategorySql(dialect, 3));

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
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialFirstAsync_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategorySql(dialect, 3, orderById: true));

        var summary = await gridReader.ReadPartialFirstAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.True(summary.CategoryId > 0);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialFirstAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategoryByIdSql(dialect),
            new { Id = -999 });

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialFirstAsync<CategorySummary>());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialFirstOrDefaultAsync_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategorySql(dialect, 3, orderById: true));

        var summary = await gridReader.ReadPartialFirstOrDefaultAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.True(summary.CategoryId > 0);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategoryByIdSql(dialect),
            new { Id = -999 });

        var summary = await gridReader.ReadPartialFirstOrDefaultAsync<CategorySummary>();

        Assert.Null(summary);
    }

    #endregion

    #region ReadPartialSingleAsync / ReadPartialSingleOrDefaultAsync

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialSingleAsync_ReturnsSingle(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategoryByIdSql(dialect),
            new { Id = 1 });

        var summary = await gridReader.ReadPartialSingleAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.CategoryId);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialSingleAsync_MultipleResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategorySql(dialect, 2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialSingleAsync<CategorySummary>());

        Assert.Contains("more than one element", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialSingleAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategoryByIdSql(dialect),
            new { Id = -999 });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await gridReader.ReadPartialSingleAsync<CategorySummary>());

        Assert.Contains("no elements", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialSingleOrDefaultAsync_ReturnsSingle(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategoryByIdSql(dialect),
            new { Id = 1 });

        var summary = await gridReader.ReadPartialSingleOrDefaultAsync<CategorySummary>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialSingleOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            SummaryCategoryByIdSql(dialect),
            new { Id = -999 });

        var summary = await gridReader.ReadPartialSingleOrDefaultAsync<CategorySummary>();

        Assert.Null(summary);
    }

    #endregion

#if NET8_0_OR_GREATER
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_DisposeAsync_ClosesReader(DialectInfo dialect)
    {
        using var connection = _fixture.GetClosedDbConnection(dialect);
        var gridReader = await connection.QueryMultipleAsync(FullCategorySql(dialect, 1));

        await gridReader.ReadFirstAsync<Category>();
        await gridReader.DisposeAsync();

        // GridReader self-opened the connection, so DisposeAsync should close it again.
        Assert.Equal(ConnectionState.Closed, connection.State);
    }
#endif

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadAsync_AfterConsumed_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(FullCategorySql(dialect, 1));

        // First read consumes the result set
        await gridReader.ReadFirstAsync<Category>();

        // Second read should throw because there are no more result sets
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await gridReader.ReadFirstAsync<Category>());
        Assert.Contains("consumed", ex.Message.ToLower());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialAsync_WithCustomMapper_UsesMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        var sql = "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id";

        using var gridReader = await connection.QueryMultipleAsync(sql, new { Id = 1 });

        var customMapper = new Func<IDataReader, Category>(reader =>
            new Category { CategoryId = Convert.ToInt32(reader.GetValue(0)), CategoryName = "PartialAsyncCustom: " + reader.GetString(1) });

        var categories = (await gridReader.ReadPartialAsync<Category>(new CommandOptions<Category>(mapper: customMapper))).ToList();

        Assert.Single(categories);
        Assert.StartsWith("PartialAsyncCustom:", categories[0].CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadScalarAsync_WithValueType_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(CountCategoriesSql(dialect));

        var count = await gridReader.ReadScalarAsync<int>();

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadScalarAsync_WithReferenceType_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync("SELECT 'test_value' AS value");

        var value = await gridReader.ReadScalarAsync<string>();

        Assert.Equal("test_value", value);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadStreamAsync_WithCustomMapper_UsesMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        var sql = "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id";

        using var gridReader = await connection.QueryMultipleAsync(sql, new { Id = 1 });

        var customMapper = new Func<IDataReader, Category>(reader =>
            new Category { CategoryId = reader.GetInt32(0), CategoryName = "ReadStreamCustom: " + reader.GetString(1) });

        var categoryList = new List<Category>();
        await foreach (var category in gridReader.ReadStreamAsync<Category>(new CommandOptions<Category>(mapper: customMapper)))
        {
            categoryList.Add(category);
        }

        Assert.Single(categoryList);
        Assert.StartsWith("ReadStreamCustom:", categoryList[0].CategoryName);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadStreamAsync_MultipleRows_UsesMapperLazyInit(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        // Get multiple rows to test the map ??= lazy initialization
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP 5 category_id, category_name FROM categories ORDER BY category_id"
            : "SELECT category_id, category_name FROM categories ORDER BY category_id LIMIT 5";

        using var gridReader = await connection.QueryMultipleAsync(sql);

        var customMapper = new Func<IDataReader, Category>(reader =>
            new Category { CategoryId = reader.GetInt32(0), CategoryName = "StreamMulti: " + reader.GetString(1) });

        var categoryList = new List<Category>();
        await foreach (var category in gridReader.ReadStreamAsync<Category>(new CommandOptions<Category>(mapper: customMapper)))
        {
            categoryList.Add(category);
        }

        Assert.Equal(5, categoryList.Count);
        Assert.All(categoryList, c => Assert.StartsWith("StreamMulti:", c.CategoryName));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialStreamAsync_MultipleRows_UsesMapperLazyInit(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        // Get multiple rows to test the map ??= lazy initialization
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP 5 category_id, category_name FROM categories ORDER BY category_id"
            : "SELECT category_id, category_name FROM categories ORDER BY category_id LIMIT 5";

        using var gridReader = await connection.QueryMultipleAsync(sql);

        var customMapper = new Func<IDataReader, Category>(reader =>
            new Category { CategoryId = reader.GetInt32(0), CategoryName = "PartialStreamMulti: " + reader.GetString(1) });

        var categoryList = new List<Category>();
        await foreach (var category in gridReader.ReadPartialStreamAsync<Category>(new CommandOptions<Category>(mapper: customMapper)))
        {
            categoryList.Add(category);
        }

        Assert.Equal(5, categoryList.Count);
        Assert.All(categoryList, c => Assert.StartsWith("PartialStreamMulti:", c.CategoryName));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadPartialStreamAsync_WithCustomMapper_UsesMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        var sql = "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id";

        using var gridReader = await connection.QueryMultipleAsync(sql, new { Id = 1 });

        var customMapper = new Func<IDataReader, Category>(reader =>
            new Category { CategoryId = reader.GetInt32(0), CategoryName = "PartialStreamAsyncCustom: " + reader.GetString(1) });

        var categoryList = new List<Category>();
        await foreach (var category in gridReader.ReadPartialStreamAsync<Category>(new CommandOptions<Category>(mapper: customMapper)))
        {
            categoryList.Add(category);
        }

        Assert.Single(categoryList);
        Assert.StartsWith("PartialStreamAsyncCustom:", categoryList[0].CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadScalarAsync_NoResults_ReturnsDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync("SELECT 1 WHERE 1 = 0");

        var result = await gridReader.ReadScalarAsync<int>();

        Assert.Equal(0, result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadScalarAsync_NullValue_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync("SELECT CAST(NULL AS INT)");

        var result = await gridReader.ReadScalarAsync<int?>();

        Assert.Null(result);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadFirstOrDefaultAsync_WithCustomMapper_UsesMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        var sql = "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id";

        using var gridReader = await connection.QueryMultipleAsync(sql, new { Id = 1 });

        var customMapper = new Func<IDataReader, Category>(reader =>
            new Category { CategoryId = reader.GetInt32(0), CategoryName = "FirstOrDefaultCustom: " + reader.GetString(1) });

        var category = await gridReader.ReadFirstOrDefaultAsync<Category>(new CommandOptions<Category>(mapper: customMapper));

        Assert.NotNull(category);
        Assert.StartsWith("FirstOrDefaultCustom:", category.CategoryName);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadAsync_MultipleRows_UsesMapperLazyInit(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);

        // Get multiple rows to test the map ??= lazy initialization
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP 5 category_id, category_name FROM categories ORDER BY category_id"
            : "SELECT category_id, category_name FROM categories ORDER BY category_id LIMIT 5";

        using var gridReader = await connection.QueryMultipleAsync(sql);

        var customMapper = new Func<IDataReader, Category>(reader =>
            new Category { CategoryId = reader.GetInt32(0), CategoryName = "ReadMulti: " + reader.GetString(1) });

        var categories = await gridReader.ReadAsync<Category>(new CommandOptions<Category>(mapper: customMapper));

        Assert.Equal(5, categories.Count);
        Assert.All(categories, c => Assert.StartsWith("ReadMulti:", c.CategoryName));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadSingleOrDefaultAsync_MultipleRows_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        // Use a query that returns multiple rows of Category-compatible data
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT TOP 2 category_id, category_name FROM categories"
            : "SELECT category_id, category_name FROM categories LIMIT 2";

        using var gridReader = await connection.QueryMultipleAsync(sql);

        // Use ReadPartialSingleOrDefaultAsync since we're only selecting 2 columns
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gridReader.ReadPartialSingleOrDefaultAsync<Category>());

        Assert.Contains("Sequence contains more than one element", ex.Message);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadScalarAsync_WithFallbackConversion(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        // Test the fallback path in ReadScalarAsync<T> when GetFieldValueAsync fails
        using var gridReader = await connection.QueryMultipleAsync("SELECT 1");

        var result = await gridReader.ReadScalarAsync<long>();

        Assert.Equal(1, result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadAsync_WithMultipleResultSets_ReadsAll(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var sql = dialect.Provider == DialectProvider.SqlServer
            ? "SELECT 1; SELECT 2; SELECT 3"
            : "SELECT 1; SELECT 2; SELECT 3";

        using var gridReader = await connection.QueryMultipleAsync(sql);

        var result1 = await gridReader.ReadScalarAsync<int>();
        var result2 = await gridReader.ReadScalarAsync<int>();
        var result3 = await gridReader.ReadScalarAsync<int>();

        Assert.Equal(1, result1);
        Assert.Equal(2, result2);
        Assert.Equal(3, result3);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task GridReader_ReadAsync_AfterAllResultSetsConsumed_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync("SELECT 1");

        // Consume the only result set
        await gridReader.ReadScalarAsync<int>();

        // Try to read again - should throw EnsureNotConsumed exception
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => gridReader.ReadScalarAsync<int>());
        Assert.Contains("consumed", ex.Message.ToLower());
    }

    private static string FullCategorySql(DialectInfo dialect, int top, bool orderById = false) =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT TOP ({top}) CategoryId, CategoryName, Description FROM Categories{(orderById ? " ORDER BY CategoryId" : string.Empty)}"
            : $"SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories{(orderById ? " ORDER BY category_id" : string.Empty)} LIMIT {top}";

    private static string SummaryCategorySql(DialectInfo dialect, int top, bool orderById = false) =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT TOP ({top}) CategoryId, CategoryName FROM Categories{(orderById ? " ORDER BY CategoryId" : string.Empty)}"
            : $"SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories{(orderById ? " ORDER BY category_id" : string.Empty)} LIMIT {top}";

    private static string FullCategoryByIdSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @Id";

    private static string SummaryCategoryByIdSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id";

    private static string CountCategoriesSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT COUNT(*) FROM Categories"
            : "SELECT COUNT(*) FROM categories";
}