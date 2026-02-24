using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialFirstAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialFirstAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstAsync_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstAsync_MissingColumn_Allowed(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstAsync_NoResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connection.QueryPartialFirstAsync<ProductSummary>(
                "SELECT product_id AS ProductId FROM products WHERE product_id = -999").AsTask());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstAsync_WithParameters_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstAsync_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();

        var product = await connection.QueryPartialFirstAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            cts.Token);

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }
}




