using System.Linq;
using System.Threading;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

public class ConnectionLifecycleStressTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public ConnectionLifecycleStressTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_HighConcurrency_DoesNotExhaustConnections(DialectInfo dialect)
    {
        const int totalOperations = 180;
        const int maxConcurrency = 24;

        using var gate = new SemaphoreSlim(maxConcurrency);

        var tasks = Enumerable.Range(0, totalOperations).Select(async _ =>
        {
            await gate.WaitAsync();
            try
            {
                using var connection = _fixture.GetDbConnection(dialect);
                var category = await connection.QueryFirstOrDefaultAsync<CategorySummary>(
                    CategoryByIdSql(dialect),
                    new { Id = 1 });

                Assert.NotNull(category);
                Assert.Equal(1, category!.CategoryId);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryMultipleAsync_HighConcurrency_DisposedGridReader_DoesNotExhaustConnections(DialectInfo dialect)
    {
        const int totalOperations = 120;
        const int maxConcurrency = 20;

        using var gate = new SemaphoreSlim(maxConcurrency);

        var tasks = Enumerable.Range(0, totalOperations).Select(async _ =>
        {
            await gate.WaitAsync();
            try
            {
                using var connection = _fixture.GetDbConnection(dialect);
                using var gridReader = await connection.QueryMultipleAsync(TwoResultSetsSql(dialect));

                var categories = await gridReader.ReadPartialAsync<CategorySummary>();
                var products = await gridReader.ReadPartialAsync<ProductSummary>();

                Assert.NotEmpty(categories);
                Assert.NotEmpty(products);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private static string CategoryByIdSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? "SELECT CategoryId AS CategoryId, CategoryName AS CategoryName FROM Categories WHERE CategoryId = @Id"
            : "SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories WHERE category_id = @Id";

    private static string TwoResultSetsSql(DialectInfo dialect) =>
        dialect.Provider == DialectProvider.SqlServer
            ? @"
                SELECT TOP (2) CategoryId AS CategoryId, CategoryName AS CategoryName FROM Categories ORDER BY CategoryId;
                SELECT TOP (2) ProductId AS ProductId, ProductName AS ProductName FROM Products ORDER BY ProductId;"
            : @"
                SELECT category_id AS CategoryId, category_name AS CategoryName FROM categories ORDER BY category_id LIMIT 2;
                SELECT product_id AS ProductId, product_name AS ProductName FROM products ORDER BY product_id LIMIT 2;";
}