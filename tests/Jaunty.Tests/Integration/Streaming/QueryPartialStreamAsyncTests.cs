using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Streaming;

public class QueryPartialStreamAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialStreamAsyncTests(DialectFixture fixture)
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
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialStreamAsync_WithResults_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = connection.QueryPartialStreamAsync<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            Assert.True(summary.ProductId > 0);
            Assert.NotNull(summary.ProductName);
            count++;
            if (count >= 5) break; // Test that it streams incrementally
        }

        Assert.Equal(5, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialStreamAsync_WithoutParameters_YieldsAll(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = connection.QueryPartialStreamAsync<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 3)}product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 3)}");

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Equal(3, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialStreamAsync_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = connection.QueryPartialStreamAsync<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
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
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialStreamAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Reasonable timeout

        var summaries = connection.QueryPartialStreamAsync<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 5)}product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 5)}",
            cts.Token);

        var count = 0;
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries.WithCancellation(cts.Token))
#else
        foreach (var summary in await summaries)
#endif
        {
            Assert.True(summary.ProductId > 0);
            count++;
        }

        Assert.Equal(5, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialStreamAsync_ExtraColumns_IgnoresExtra(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = connection.QueryPartialStreamAsync<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 5)}product_id AS ProductId, product_name AS ProductName, unit_price, supplier_id FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 5)}");

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Equal(5, list.Count);
        Assert.All(list, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialStreamAsync_MissingColumns_SetsDefaults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = connection.QueryPartialStreamAsync<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 5)}product_id AS ProductId FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 5)}");

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Equal(5, list.Count);
        Assert.All(list, s =>
        {
            Assert.True(s.ProductId > 0);
            Assert.Equal(string.Empty, s.ProductName); // Default value
        });
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialStreamAsync_EmptyResult_YieldsNothing(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = connection.QueryPartialStreamAsync<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE product_id = @Id",
            new { Id = -999 });

        var list = new List<ProductSummary>();
#if ASYNC_ENUMERABLE_SUPPORT
        await foreach (var summary in summaries)
#else
        foreach (var summary in await summaries)
#endif
        {
            list.Add(summary);
        }

        Assert.Empty(list);
    }
}
