using Jaunty;
using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Sqlite.Multiple;

public class QueryMultipleAsyncTests : IDisposable
{
    private readonly Database _db;

    public QueryMultipleAsyncTests()
    {
        _db = new Database();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Dispose();
    }

    [Fact]
    public async Task QueryMultipleAsync_ReturnsMultipleResultSets()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT 2; SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 3");

        var categories = (await gridReader.ReadAsync<Category>()).ToList();
        var products = (await gridReader.ReadAsync<ProductSummary>()).ToList();

        Assert.Equal(2, categories.Count);
        Assert.Equal(3, products.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
        Assert.All(products, p => Assert.NotNull(p.ProductName));
    }

    [Fact]
    public async Task QueryMultipleAsync_WithParameters_FiltersCorrectly()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @CategoryId; SELECT product_id AS ProductId, product_name AS ProductName FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var categories = (await gridReader.ReadAsync<Category>()).ToList();
        var products = (await gridReader.ReadAsync<ProductSummary>()).ToList();

        Assert.Single(categories);
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal(1, p.CategoryId));
    }

    [Fact]
    public async Task QueryMultipleAsync_WithCommandOptions_Works()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories; SELECT COUNT(*) FROM products",
            CommandOptions.WithTimeout(30));

        var categoryCount = await gridReader.ReadScalarAsync<long>();
        var productCount = await gridReader.ReadScalarAsync<long>();

        Assert.True(categoryCount > 0);
        Assert.True(productCount > 0);
    }

    [Fact]
    public async Task QueryMultipleAsync_WithCancellationToken_Works()
    {
        using var cts = new CancellationTokenSource();
        
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1; SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 1",
            cancellationToken: cts.Token);

        var categories = (await gridReader.ReadAsync<Category>()).ToList();
        var products = (await gridReader.ReadAsync<ProductSummary>()).ToList();

        Assert.Single(categories);
        Assert.Single(products);
    }

    [Fact]
    public async Task QueryMultipleAsync_WithTransaction_Works()
    {
        _db.Connection.Open();
        using var transaction = _db.Connection.BeginTransaction();

        try
        {
            using var gridReader = await _db.Connection.QueryMultipleAsync(
                "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1; SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 1",
                CommandOptions.WithTransaction(transaction));

            var categories = (await gridReader.ReadAsync<Category>()).ToList();
            var products = (await gridReader.ReadAsync<ProductSummary>()).ToList();

            Assert.Single(categories);
            Assert.Single(products);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Fact]
    public async Task QueryMultipleAsync_PartialRead_DoesNotThrow()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT 1; SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT 1");

        // Only read first result set, not second
        var categories = (await gridReader.ReadAsync<Category>()).ToList();

        Assert.Single(categories);
        // Second result set is automatically disposed when GridReader is disposed
    }

    [Fact]
    public async Task QueryMultipleAsync_ReadScalar_WithParameters_Works()
    {
        using var gridReader = await _db.Connection.QueryMultipleAsync(
            "SELECT COUNT(*) FROM categories; SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
            new { CategoryId = 1 });

        var categoryCount = await gridReader.ReadScalarAsync<long>();
        var productCount = await gridReader.ReadScalarAsync<long>();

        Assert.True(categoryCount > 0);
        Assert.True(productCount > 0);
    }
}
