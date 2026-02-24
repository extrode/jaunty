using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Multiple;

public class GridReaderTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public GridReaderTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_Read_ReturnsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategorySql(dialect, 2));

        var categories = gridReader.Read<Category>().ToList();

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartial_AllowsMissingColumns(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategorySql(dialect, 2));

        var categories = gridReader.ReadPartial<CategorySummary>().ToList(); // CategorySummary has fewer properties

        Assert.Equal(2, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadFirst_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategorySql(dialect, 1, orderById: true));

        var category = gridReader.ReadFirst<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadFirstOrDefault_ReturnsFirstOrNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategorySql(dialect, 1, orderById: true));

        var category = gridReader.ReadFirstOrDefault<Category>();

        Assert.NotNull(category);
        Assert.True(category.CategoryId > 0);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadFirstOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategoryByIdSql(dialect),
            new { Id = -999 });

        var category = gridReader.ReadFirstOrDefault<Category>();

        Assert.Null(category);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadSingle_ReturnsSingle(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategoryByIdSql(dialect),
            new { Id = 1 });

        var category = gridReader.ReadSingle<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadSingle_MultipleResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategorySql(dialect, 2));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            gridReader.ReadSingle<Category>());

        Assert.Contains("more than one element", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadSingle_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategoryByIdSql(dialect),
            new { Id = -999 });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            gridReader.ReadSingle<Category>());

        Assert.Contains("no elements", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadSingleOrDefault_ReturnsSingleOrDefault(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategoryByIdSql(dialect),
            new { Id = 1 });

        var category = gridReader.ReadSingleOrDefault<Category>();

        Assert.NotNull(category);
        Assert.Equal(1, category.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadSingleOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategoryByIdSql(dialect),
            new { Id = -999 });

        var category = gridReader.ReadSingleOrDefault<Category>();

        Assert.Null(category);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadScalar_ReturnsValue(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            CountCategoriesSql(dialect));

        var count = gridReader.ReadScalar<long>();

        Assert.True(count > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadStream_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            FullCategorySql(dialect, 3));

        var categories = gridReader.ReadStream<Category>().ToList();

        Assert.Equal(3, categories.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialStream_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategorySql(dialect, 3));

        var summaries = gridReader.ReadPartialStream<CategorySummary>().ToList();

        Assert.Equal(3, summaries.Count);
        Assert.All(summaries, s => Assert.NotNull(s.CategoryName));
    }

    #region ReadPartialFirst / ReadPartialFirstOrDefault

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialFirst_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategorySql(dialect, 3, orderById: true));

        var summary = gridReader.ReadPartialFirst<CategorySummary>();

        Assert.NotNull(summary);
        Assert.True(summary.CategoryId > 0);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialFirst_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategoryByIdSql(dialect),
            new { Id = -999 });

        Assert.Throws<InvalidOperationException>(() =>
            gridReader.ReadPartialFirst<CategorySummary>());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialFirstOrDefault_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategorySql(dialect, 3, orderById: true));

        var summary = gridReader.ReadPartialFirstOrDefault<CategorySummary>();

        Assert.NotNull(summary);
        Assert.True(summary.CategoryId > 0);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialFirstOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategoryByIdSql(dialect),
            new { Id = -999 });

        var summary = gridReader.ReadPartialFirstOrDefault<CategorySummary>();

        Assert.Null(summary);
    }

    #endregion

    #region ReadPartialSingle / ReadPartialSingleOrDefault

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialSingle_ReturnsSingle(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategoryByIdSql(dialect),
            new { Id = 1 });

        var summary = gridReader.ReadPartialSingle<CategorySummary>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.CategoryId);
        Assert.NotNull(summary.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialSingle_MultipleResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategorySql(dialect, 2));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            gridReader.ReadPartialSingle<CategorySummary>());

        Assert.Contains("more than one element", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialSingle_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategoryByIdSql(dialect),
            new { Id = -999 });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            gridReader.ReadPartialSingle<CategorySummary>());

        Assert.Contains("no elements", ex.Message);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialSingleOrDefault_ReturnsSingle(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategoryByIdSql(dialect),
            new { Id = 1 });

        var summary = gridReader.ReadPartialSingleOrDefault<CategorySummary>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void GridReader_ReadPartialSingleOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var gridReader = connection.QueryMultiple(
            SummaryCategoryByIdSql(dialect),
            new { Id = -999 });

        var summary = gridReader.ReadPartialSingleOrDefault<CategorySummary>();

        Assert.Null(summary);
    }

    #endregion

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void GridReader_ReadPartial_WithCustomMapper_UsesMapper(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        var sql = "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id";

        using var gridReader = connection.QueryMultiple(sql, new { Id = 1 });

        var customMapper = new Func<IDataReader, Category>(reader =>
            new Category { CategoryId = reader.GetInt32(0), CategoryName = "Custom: " + reader.GetString(1) });

        var categories = gridReader.Read<Category>(new CommandOptions<Category>(mapper: customMapper)).ToList();

        Assert.Single(categories);
        Assert.StartsWith("Custom:", categories[0].CategoryName);
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

