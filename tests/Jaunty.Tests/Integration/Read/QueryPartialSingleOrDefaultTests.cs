using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialSingleOrDefaultTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryPartialSingleOrDefaultTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingleOrDefault_WithExactlyOneResult_ReturnsEntity(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialSingleOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
        Assert.False(string.IsNullOrEmpty(product.ProductName));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingleOrDefault_MissingColumn_Allowed(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialSingleOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingleOrDefault_NoResults_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialSingleOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -999");

        Assert.Null(product);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingleOrDefault_MultipleResults_Throws(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        Assert.Throws<InvalidOperationException>(() =>
            connection.QueryPartialSingleOrDefault<ProductSummary>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products"));
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingleOrDefault_WithParameters_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialSingleOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingleOrDefault_WithParametersAndOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialSingleOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingleOrDefault_WithOptionsOnly_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialSingleOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void QueryPartialSingleOrDefault_NoResults_WithParameters_ReturnsNull(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var product = connection.QueryPartialSingleOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }
}



