using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryPartialFirstOrDefaultAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialFirstOrDefaultAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstOrDefaultAsync_WithResults_ReturnsFirst()
    {
        var product = await _db.Connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstOrDefaultAsync_MissingColumn_Allowed()
    {
        var product = await _db.Connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _db.Connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -999");

        Assert.Null(product);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstOrDefaultAsync_WithParameters_FiltersCorrectly()
    {
        var product = await _db.Connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstOrDefaultAsync_WithParametersAndOptions_Works()
    {
        var product = await _db.Connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
        Assert.Equal(1, product.CategoryId);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstOrDefaultAsync_WithOptionsOnly_Works()
    {
        var product = await _db.Connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.True(product.ProductId > 0);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialFirstOrDefaultAsync_NoResults_WithParameters_ReturnsNull()
    {
        var product = await _db.Connection.QueryPartialFirstOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }
}
