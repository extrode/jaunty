using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialAsync_MissingColumn_Allowed(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = await connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.False(string.IsNullOrEmpty(s.ProductName)));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialAsync_WithParameter_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = await connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { Id = 1 });

        Assert.NotEmpty(summaries);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialAsync_WithOptions_ReturnsEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = await connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialAsync_WithParametersAndOptions_ReturnsFilteredEntities(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var summaries = await connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { Id = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();

        var summaries = await connection.QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            cts.Token);

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.False(string.IsNullOrEmpty(s.ProductName)));
    }
}


