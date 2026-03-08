using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Streaming;

public class QueryPartialUnbufferedTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialUnbufferedTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static string ProductsTable(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer ? "Products" : "products";

    private static string TopPrefix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? $"TOP ({count}) " : string.Empty;

    private static string LimitSuffix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? string.Empty : $" LIMIT {count}";

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartialUnbuffered_WithResults_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartialUnbuffered<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var count = 0;
        foreach (var summary in summaries)
        {
            Assert.True(summary.ProductId > 0);
            Assert.NotNull(summary.ProductName);
            count++;
            if (count >= 5) break;
        }

        Assert.Equal(5, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartialUnbuffered_WithoutParameters_YieldsAll(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartialUnbuffered<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 3)}product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 3)}");

        var list = summaries.ToList();
        Assert.Equal(3, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartialUnbuffered_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartialUnbuffered<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE category_id = @CategoryId",
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

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartialUnbuffered_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartialUnbuffered<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 3)}product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 3)}",
            CommandOptions<ProductSummary>.WithTimeout(30));

        var list = summaries.ToList();
        Assert.Equal(3, list.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartialUnbuffered_MissingColumns_SetsDefaults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartialUnbuffered<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 5)}product_id AS ProductId FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 5)}");

        var list = summaries.ToList();
        Assert.Equal(5, list.Count);
        Assert.All(list, s =>
        {
            Assert.True(s.ProductId > 0);
            Assert.Equal(string.Empty, s.ProductName);
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryPartialUnbuffered_EmptyResult_YieldsNothing(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var summaries = connection.QueryPartialUnbuffered<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE product_id = @Id",
            new { Id = -999 });

        var list = summaries.ToList();
        Assert.Empty(list);
    }
}