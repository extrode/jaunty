using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

public class QueryPartialSingleTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialSingleTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public void QueryPartialSingle_WithExactlyOneResult_ReturnsEntity()
    {
        var product = _db.Connection.QueryPartialSingle<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
        Assert.False(string.IsNullOrEmpty(product.ProductName));
    }

    [Fact]
    public void QueryPartialSingle_MissingColumn_Allowed()
    {
        var product = _db.Connection.QueryPartialSingle<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [Fact]
    public void QueryPartialSingle_NoResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QueryPartialSingle<ProductSummary>(
                "SELECT product_id AS ProductId FROM products WHERE product_id = -999"));
    }

    [Fact]
    public void QueryPartialSingle_MultipleResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _db.Connection.QueryPartialSingle<ProductSummary>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products"));
    }

    [Fact]
    public void QueryPartialSingle_WithParameters_FiltersCorrectly()
    {
        var product = _db.Connection.QueryPartialSingle<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.Equal(2, product.ProductId);
    }

    [Fact]
    public void QueryPartialSingle_WithParametersAndOptions_Works()
    {
        var product = _db.Connection.QueryPartialSingle<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.Equal(2, product.ProductId);
    }

    [Fact]
    public void QueryPartialSingle_WithOptionsOnly_Works()
    {
        var product = _db.Connection.QueryPartialSingle<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }
}

