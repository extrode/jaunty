using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialSingleOrDefaultAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialSingleOrDefaultAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithExactlyOneResult_ReturnsEntity(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_MissingColumn_Allowed(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -999");

        Assert.Null(product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_MultipleResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products").AsTask());
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithParameters_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_NoResults_WithParameters_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();

        var product = await connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2",
            cts.Token);

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }
}




