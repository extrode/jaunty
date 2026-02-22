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
    [MicrosoftSqlite]
    public async Task QueryPartialAsync_MissingColumn_Allowed(DialectInfo dialect)
    {
        var summaries = await _fixture.GetDbConnection(dialect).QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.False(string.IsNullOrEmpty(s.ProductName)));
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialAsync_WithParameter_FiltersCorrectly(DialectInfo dialect)
    {
        var summaries = await _fixture.GetDbConnection(dialect).QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { Id = 1 });

        Assert.NotEmpty(summaries);
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialAsync_WithOptions_ReturnsEntities(DialectInfo dialect)
    {
        var summaries = await _fixture.GetDbConnection(dialect).QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }

    [Theory]
    [MicrosoftSqlite]
    public async Task QueryPartialAsync_WithParametersAndOptions_ReturnsFilteredEntities(DialectInfo dialect)
    {
        var summaries = await _fixture.GetDbConnection(dialect).QueryPartialAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @Id",
            new { Id = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotEmpty(summaries);
        Assert.All(summaries, s => Assert.True(s.ProductId > 0));
    }
}


