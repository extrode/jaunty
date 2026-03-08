using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Multiple;

public class QueryMultipleAsyncTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryMultipleAsyncTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryMultipleAsync_ReturnsMultipleResultSets(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            CategoryProductMultipleSql(dialect, 2, 3));

        var categories = (await gridReader.ReadAsync<Category>()).ToList();
        var products = (await gridReader.ReadPartialAsync<ProductSummary>()).ToList();

        Assert.Equal(2, categories.Count);
        Assert.Equal(3, products.Count);
        Assert.All(categories, c => Assert.NotNull(c.CategoryName));
        Assert.All(products, p => Assert.NotNull(p.ProductName));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryMultipleAsync_WithParameters_FiltersCorrectly(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            CategoryProductByCategorySql(dialect),
            new { CategoryId = 1 });

        var categories = (await gridReader.ReadAsync<Category>()).ToList();
        var products = (await gridReader.ReadAsync<ProductSummary>()).ToList();

        Assert.Single(categories);
        Assert.NotEmpty(products);
        Assert.All(products, p => Assert.Equal(1, p.CategoryId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryMultipleAsync_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            CategoryProductCountSql(dialect),
            CommandOptions.WithTimeout(30));

        var categoryCount = await gridReader.ReadScalarAsync<long>();
        var productCount = await gridReader.ReadScalarAsync<long>();

        Assert.True(categoryCount > 0);
        Assert.True(productCount > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryMultipleAsync_WithCancellationToken_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var cts = new CancellationTokenSource();

        using var gridReader = await connection.QueryMultipleAsync(
            CategoryProductSummarySql(dialect, 1, 1),
            cancellationToken: cts.Token);

        var categories = (await gridReader.ReadPartialAsync<Category>()).ToList();
        var products = (await gridReader.ReadPartialAsync<ProductSummary>()).ToList();

        Assert.Single(categories);
        Assert.Single(products);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryMultipleAsync_WithTransaction_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var transaction = connection.BeginTransaction();

        try
        {
            using var gridReader = await connection.QueryMultipleAsync(
                CategoryProductSummarySql(dialect, 1, 1),
                CommandOptions.WithTransaction(transaction));

            var categories = (await gridReader.ReadPartialAsync<Category>()).ToList();
            var products = (await gridReader.ReadPartialAsync<ProductSummary>()).ToList();

            Assert.Single(categories);
            Assert.Single(products);
        }
        finally
        {
            transaction.Rollback();
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryMultipleAsync_PartialRead_DoesNotThrow(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            CategoryProductSummarySql(dialect, 1, 1));

        // Only read first result set, not second
        var categories = (await gridReader.ReadPartialAsync<Category>()).ToList();

        Assert.Single(categories);
        // Second result set is automatically disposed when GridReader is disposed
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task QueryMultipleAsync_ReadScalar_WithParameters_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        using var gridReader = await connection.QueryMultipleAsync(
            CategoryCountAndProductsByCategorySql(dialect),
            new { CategoryId = 1 });

        var categoryCount = await gridReader.ReadScalarAsync<long>();
        var productCount = await gridReader.ReadScalarAsync<long>();

        Assert.True(categoryCount > 0);
        Assert.True(productCount > 0);
    }

    private static string CategoryProductMultipleSql(DialectInfo dialect, int categoryTop, int productTop) =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT TOP ({categoryTop}) CategoryId, CategoryName, Description FROM Categories; SELECT TOP ({productTop}) ProductId, ProductName FROM Products;"
            : $"SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories LIMIT {categoryTop}; SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT {productTop};";

    private static string CategoryProductSummarySql(DialectInfo dialect, int categoryTop, int productTop) =>
        dialect.Provider == DialectProvider.SqlServer
            ? $"SELECT TOP ({categoryTop}) CategoryId, CategoryName FROM Categories; SELECT TOP ({productTop}) ProductId, ProductName FROM Products;"
            : $"SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories LIMIT {categoryTop}; SELECT product_id AS ProductId, product_name AS ProductName FROM products LIMIT {productTop};";

    private static string CategoryProductByCategorySql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId, CategoryName, Description FROM Categories WHERE CategoryId = @CategoryId; SELECT ProductId, ProductName, CategoryId FROM Products WHERE CategoryId = @CategoryId"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName, description AS Description FROM categories WHERE category_id = @CategoryId; SELECT product_id AS ProductId, product_name AS ProductName, category_id AS CategoryId FROM products WHERE category_id = @CategoryId";

    private static string CategoryProductCountSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT COUNT(*) FROM Categories; SELECT COUNT(*) FROM Products"
            : "SELECT COUNT(*) FROM categories; SELECT COUNT(*) FROM products";

    private static string CategoryCountAndProductsByCategorySql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT COUNT(*) FROM Categories; SELECT COUNT(*) FROM Products WHERE CategoryId = @CategoryId"
            : "SELECT COUNT(*) FROM categories; SELECT COUNT(*) FROM products WHERE category_id = @CategoryId";
}