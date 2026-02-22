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
    [MicrosoftSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithExactlyOneResult_ReturnsEntity(DialectInfo dialect)
    {
        var product = await _fixture.GetDbConnection(dialect).QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_MissingColumn_Allowed(DialectInfo dialect)
    {
        var product = await _fixture.GetDbConnection(dialect).QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_NoResults_ReturnsNull(DialectInfo dialect)
    {
        var product = await _fixture.GetDbConnection(dialect).QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -999");

        Assert.Null(product);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_MultipleResults_Throws(DialectInfo dialect)
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _fixture.GetDbConnection(dialect).QueryPartialSingleOrDefaultAsync<ProductSummary>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products").AsTask());
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithParameters_FiltersCorrectly(DialectInfo dialect)
    {
        var product = await _fixture.GetDbConnection(dialect).QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        var product = await _fixture.GetDbConnection(dialect).QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_WithOptionsOnly_Works(DialectInfo dialect)
    {
        var product = await _fixture.GetDbConnection(dialect).QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialSingleOrDefaultAsync_NoResults_WithParameters_ReturnsNull(DialectInfo dialect)
    {
        var product = await _fixture.GetDbConnection(dialect).QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }
}




