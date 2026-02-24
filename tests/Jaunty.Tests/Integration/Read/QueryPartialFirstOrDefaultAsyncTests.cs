using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialFirstOrDefaultAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialFirstOrDefaultAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstOrDefaultAsync_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
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
    public async Task QueryPartialFirstOrDefaultAsync_MissingColumn_Allowed(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
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
    public async Task QueryPartialFirstOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -999");

        Assert.Null(product);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstOrDefaultAsync_WithParameters_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryPartialFirstOrDefaultAsync_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
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
    public async Task QueryPartialFirstOrDefaultAsync_NoResults_WithParameters_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        var product = await connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }
}



