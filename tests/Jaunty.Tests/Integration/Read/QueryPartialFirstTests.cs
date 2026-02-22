using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialFirstTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialFirstTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryPartialFirst_WithResults_ReturnsFirst()
    {
        var product = _db.Connection.QueryPartialFirst<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.False(string.IsNullOrEmpty(product.ProductName));
    }

    [Fact]
    public void QueryPartialFirst_MissingColumn_Allowed()
    {
        var product = _db.Connection.QueryPartialFirst<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [Fact]
    public void QueryPartialFirst_NoResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QueryPartialFirst<ProductSummary>(
                "SELECT product_id AS ProductId FROM products WHERE product_id = -999"));
    }

    [Fact]
    public void QueryPartialFirst_WithParameters_FiltersCorrectly()
    {
        var product = _db.Connection.QueryPartialFirst<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Fact]
    public void QueryPartialFirst_WithParametersAndOptions_Works()
    {
        var product = _db.Connection.QueryPartialFirst<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [Fact]
    public void QueryPartialFirst_WithOptionsOnly_Works()
    {
        var product = _db.Connection.QueryPartialFirst<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }
}

