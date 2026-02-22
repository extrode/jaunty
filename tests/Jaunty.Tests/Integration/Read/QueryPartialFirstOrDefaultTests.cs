using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialFirstOrDefaultTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialFirstOrDefaultTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryPartialFirstOrDefault_WithResults_ReturnsFirst()
    {
        var product = _db.Connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.False(string.IsNullOrEmpty(product.ProductName));
    }

    [Fact]
    public void QueryPartialFirstOrDefault_MissingColumn_Allowed()
    {
        var product = _db.Connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [Fact]
    public void QueryPartialFirstOrDefault_NoResults_ReturnsNull()
    {
        var product = _db.Connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -999");

        Assert.Null(product);
    }

    [Fact]
    public void QueryPartialFirstOrDefault_WithParameters_FiltersCorrectly()
    {
        var product = _db.Connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Fact]
    public void QueryPartialFirstOrDefault_WithParametersAndOptions_Works()
    {
        var product = _db.Connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Fact]
    public void QueryPartialFirstOrDefault_WithOptionsOnly_Works()
    {
        var product = _db.Connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [Fact]
    public void QueryPartialFirstOrDefault_NoResults_WithParameters_ReturnsNull()
    {
        var product = _db.Connection.QueryPartialFirstOrDefault<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }
}

