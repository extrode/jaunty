using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialFirstOrDefaultTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialFirstOrDefaultTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialFirstOrDefault_WithResults_ReturnsFirst(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.False(string.IsNullOrEmpty(product.ProductName));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialFirstOrDefault_MissingColumn_Allowed(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialFirstOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -999");

        Assert.Null(product);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialFirstOrDefault_WithParameters_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialFirstOrDefault_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialFirstOrDefault_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialFirstOrDefault_NoResults_WithParameters_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }
}



