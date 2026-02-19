using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Read;

public class QueryPartialSingleOrDefaultAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryPartialSingleOrDefaultAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialSingleOrDefaultAsync_WithExactlyOneResult_ReturnsEntity()
    {
        var product = await _db.Connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialSingleOrDefaultAsync_MissingColumn_Allowed()
    {
        var product = await _db.Connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2");

        Assert.NotNull(product);
        Assert.Equal(0, product.CategoryId);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialSingleOrDefaultAsync_NoResults_ReturnsNull()
    {
        var product = await _db.Connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = -999");

        Assert.Null(product);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialSingleOrDefaultAsync_MultipleResults_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _db.Connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
                "SELECT product_id AS ProductId, product_name AS ProductName FROM products").AsTask());
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialSingleOrDefaultAsync_WithParameters_FiltersCorrectly()
    {
        var product = await _db.Connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 });

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialSingleOrDefaultAsync_WithParametersAndOptions_Works()
    {
        var product = await _db.Connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE product_id = @Id",
            new { Id = 2 },
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialSingleOrDefaultAsync_WithOptionsOnly_Works()
    {
        var product = await _db.Connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE product_id = 2",
            CommandOptions<ProductSummary>.WithTimeout(30));

        Assert.NotNull(product);
        Assert.Equal(2, product.ProductId);
    }

    [SkipSQLiteAsyncFact]
    public async Task QueryPartialSingleOrDefaultAsync_NoResults_WithParameters_ReturnsNull()
    {
        var product = await _db.Connection.QueryPartialSingleOrDefaultAsync<ProductSummary>(
            "SELECT product_id AS ProductId FROM products WHERE product_id = @Id",
            new { Id = -999 });

        Assert.Null(product);
    }
}

