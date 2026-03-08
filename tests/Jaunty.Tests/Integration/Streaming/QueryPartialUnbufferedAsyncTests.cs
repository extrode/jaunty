#if NET8_0_OR_GREATER
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Streaming;

public class QueryPartialUnbufferedAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialUnbufferedAsyncTests(DialectFixture fixture)
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
    public async Task QueryPartialUnbufferedAsync_WithResults_YieldsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var count = 0;
        await foreach (var summary in connection.QueryPartialUnbufferedAsync<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE category_id = @CategoryId",
            new { CategoryId = 1 }))
        {
            Assert.True(summary.ProductId > 0);
            count++;
            if (count >= 5) break;
        }

        Assert.Equal(5, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialUnbufferedAsync_WithoutParameters_YieldsAll(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var count = 0;
        await foreach (var summary in connection.QueryPartialUnbufferedAsync<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 3)}product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 3)}"))
        {
            Assert.True(summary.ProductId > 0);
            count++;
        }

        Assert.Equal(3, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialUnbufferedAsync_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var count = 0;
        await foreach (var summary in connection.QueryPartialUnbufferedAsync<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30)))
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
    public async Task QueryPartialUnbufferedAsync_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var count = 0;
        await foreach (var summary in connection.QueryPartialUnbufferedAsync<ProductSummary>(
            $"SELECT {TopPrefix(dialect, 3)}product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)}{LimitSuffix(dialect, 3)}",
            CommandOptions<ProductSummary>.WithTimeout(30)))
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialUnbufferedAsync_EmptyResult_YieldsNothing(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var count = 0;
        await foreach (var _ in connection.QueryPartialUnbufferedAsync<ProductSummary>(
            $"SELECT product_id AS ProductId, product_name AS ProductName FROM {ProductsTable(dialect)} WHERE product_id = @Id",
            new { Id = -999 }))
        {
            count++;
        }

        Assert.Equal(0, count);
    }
}
#endif