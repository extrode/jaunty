using Jaunty.Core;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// Tests for the <see cref="MultiEntityCommandOptions{T1, T2}"/> overloads of
/// Query/QueryFirst/QueryFirstOrDefault/QuerySingle/QuerySingleOrDefault/QueryStream
/// (sync and async). AUD-R7 batch-01: these overloads previously did not exist for arity 2,
/// unlike arity 3-7's plain-<see cref="CommandOptions"/> equivalents.
/// </summary>
public class QueryMultiEntityCommandOptionsTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public QueryMultiEntityCommandOptionsTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static string TopPrefix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? $"TOP ({count}) " : string.Empty;

    private static string LimitSuffix(DialectInfo dialect, int count) =>
        dialect.Provider == DialectProvider.SqlServer ? string.Empty : $" LIMIT {count}";

    private const string JoinSql = @"
                p.product_id AS ProductId,
                p.product_name AS ProductName,
                c.category_id AS CategoryId,
                c.category_name AS CategoryName
              FROM products p
              JOIN categories c ON p.category_id = c.category_id";

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TwoEntities_WithMultiEntityCommandOptions_UsesTransaction(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var txn = connection.BeginTransaction();

        // Mutate product 1's name inside the transaction, via the same transaction.
        connection.Execute(
            "UPDATE products SET product_name = @Name WHERE product_id = @Id",
            new { Name = "TXN-SENTINEL", Id = 1 },
            new CommandOptions(transaction: txn));

        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>(transaction: txn);

        var results = connection.Query<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = 1", options);

        // If MultiEntityCommandOptions silently dropped the transaction when converting to the
        // underlying CommandOptions, this read would run outside the pending transaction and would
        // not observe the uncommitted UPDATE above (or would throw, on providers that reject a
        // command with no Transaction set while the connection has a pending transaction).
        Assert.Single(results);
        Assert.Equal("TXN-SENTINEL", results[0].Item1.ProductName);

        txn.Rollback();

        using var verifyConnection = _fixture.GetConnection(dialect);
        var stillSentinel = verifyConnection.QueryScalar<long>(
            "SELECT COUNT(*) FROM products WHERE product_id = 1 AND product_name = @Name",
            new { Name = "TXN-SENTINEL" });
        Assert.Equal(0, stillSentinel);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TwoEntities_WithParametersAndMultiEntityCommandOptions_FiltersAndUsesTransaction(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        using var txn = connection.BeginTransaction();
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>(transaction: txn);

        var results = connection.Query<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE c.category_id = @CategoryId",
            new { CategoryId = 1 },
            options);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(1, r.Item2.CategoryId));
        txn.Rollback();
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirst_TwoEntities_WithMultiEntityCommandOptions_ReturnsFirstRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var (product, category) = connection.QueryFirst<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} ORDER BY p.product_id", options);

        Assert.Equal(1, product.ProductId);
        Assert.NotNull(category.CategoryName);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryFirstOrDefault_TwoEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>(commandTimeout: 30);

        var result = connection.QueryFirstOrDefault<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = @ProductId",
            new { ProductId = -999 },
            options);

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QuerySingle_TwoEntities_WithMultiEntityCommandOptions_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var (product, category) = connection.QuerySingle<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = 1", options);

        Assert.Equal(1, product.ProductId);
        Assert.True(category.CategoryId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QuerySingleOrDefault_TwoEntities_WithParametersAndMultiEntityCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var result = connection.QuerySingleOrDefault<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = @ProductId",
            new { ProductId = 1 },
            options);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void QueryStream_TwoEntities_WithMultiEntityCommandOptions_StreamsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var results = connection.QueryStream<ProductInfo, CategoryInfo>(
            $"SELECT {TopPrefix(dialect, 3)}{JoinSql}{LimitSuffix(dialect, 3)}", options).ToList();

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_TwoEntities_WithMultiEntityCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var results = await connection.QueryAsync<ProductInfo, CategoryInfo>(
            $"SELECT {TopPrefix(dialect, 3)}{JoinSql}{LimitSuffix(dialect, 3)}",
            options,
            CancellationToken.None);

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_TwoEntities_WithParametersAndMultiEntityCommandOptions_Filters(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var results = await connection.QueryAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE c.category_id = @CategoryId",
            new { CategoryId = 1 },
            options,
            CancellationToken.None);

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(1, r.Item2.CategoryId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstAsync_TwoEntities_WithMultiEntityCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var (product, _) = await connection.QueryFirstAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} ORDER BY p.product_id", options, CancellationToken.None);

        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstAsync_TwoEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var (product, _) = await connection.QueryFirstAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} ORDER BY p.product_id", options, CancellationToken.None);

        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstAsync_TwoEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var (product, _) = await connection.QueryFirstAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = @ProductId",
            new { ProductId = 1 },
            options,
            CancellationToken.None);

        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstOrDefaultAsync_TwoEntities_WithParametersAndMultiEntityCommandOptions_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var result = await connection.QueryFirstOrDefaultAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = @ProductId",
            new { ProductId = -999 },
            options,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstOrDefaultAsync_TwoEntities_WithCommandOptions_ReturnsFirstTuple(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var result = await connection.QueryFirstOrDefaultAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} ORDER BY p.product_id", options, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryFirstOrDefaultAsync_TwoEntities_WithParametersAndCommandOptions_ReturnsNullWhenEmpty(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var result = await connection.QueryFirstOrDefaultAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = @ProductId",
            new { ProductId = -999 },
            options,
            CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QuerySingleAsync_TwoEntities_WithMultiEntityCommandOptions_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var (product, category) = await connection.QuerySingleAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = 1", options, CancellationToken.None);

        Assert.Equal(1, product.ProductId);
        Assert.True(category.CategoryId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QuerySingleAsync_TwoEntities_WithCommandOptions_ReturnsSingleRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var (product, category) = await connection.QuerySingleAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = 1", options, CancellationToken.None);

        Assert.Equal(1, product.ProductId);
        Assert.True(category.CategoryId > 0);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QuerySingleAsync_TwoEntities_WithParametersAndCommandOptions_FiltersRow(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var (product, _) = await connection.QuerySingleAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = @ProductId",
            new { ProductId = 1 },
            options,
            CancellationToken.None);

        Assert.Equal(1, product.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QuerySingleOrDefaultAsync_TwoEntities_WithParametersAndMultiEntityCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var result = await connection.QuerySingleOrDefaultAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = @ProductId",
            new { ProductId = 1 },
            options,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QuerySingleOrDefaultAsync_TwoEntities_WithCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var result = await connection.QuerySingleOrDefaultAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = 1", options, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QuerySingleOrDefaultAsync_TwoEntities_WithParametersAndCommandOptions_Works(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var result = await connection.QuerySingleOrDefaultAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE p.product_id = @ProductId",
            new { ProductId = 1 },
            options,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.Value.Item1.ProductId);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryStreamAsync_TwoEntities_WithMultiEntityCommandOptions_StreamsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var results = new List<(ProductInfo, CategoryInfo)>();
        await foreach (var row in connection.QueryStreamAsync<ProductInfo, CategoryInfo>(
            $"SELECT {TopPrefix(dialect, 3)}{JoinSql}{LimitSuffix(dialect, 3)}", options, CancellationToken.None))
        {
            results.Add(row);
        }

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryStreamAsync_TwoEntities_WithCommandOptions_StreamsResults(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var results = new List<(ProductInfo, CategoryInfo)>();
        await foreach (var row in connection.QueryStreamAsync<ProductInfo, CategoryInfo>(
            $"SELECT {TopPrefix(dialect, 3)}{JoinSql}{LimitSuffix(dialect, 3)}", options, CancellationToken.None))
        {
            results.Add(row);
        }

        Assert.Equal(3, results.Count);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryStreamAsync_TwoEntities_WithParametersAndCommandOptions_Filters(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new CommandOptions<(ProductInfo, CategoryInfo)>();

        var results = new List<(ProductInfo, CategoryInfo)>();
        await foreach (var row in connection.QueryStreamAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE c.category_id = @CategoryId",
            new { CategoryId = 1 },
            options,
            CancellationToken.None))
        {
            results.Add(row);
        }

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(1, r.Item2.CategoryId));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryStreamAsync_TwoEntities_WithParametersAndMultiEntityCommandOptions_Filters(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = new MultiEntityCommandOptions<ProductInfo, CategoryInfo>();

        var results = new List<(ProductInfo, CategoryInfo)>();
        await foreach (var row in connection.QueryStreamAsync<ProductInfo, CategoryInfo>(
            $"SELECT {JoinSql} WHERE c.category_id = @CategoryId",
            new { CategoryId = 1 },
            options,
            CancellationToken.None))
        {
            results.Add(row);
        }

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(1, r.Item2.CategoryId));
    }

    // AUD-R12: QueryMultiEntityCore<T1,T2>/QueryMultiEntityCoreAsync<T1,T2> hardcoded
    // JauntyConfig.QueryResultCapacity for the results list instead of honoring
    // options.ExpectedRowCount, unlike the single-entity Query<T> path.
    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public void Query_TwoEntities_WithExpectedRowCount_PreSizesListCapacity(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = CommandOptions<(ProductInfo, CategoryInfo)>.WithExpectedRowCount(500);

        var results = connection.Query<ProductInfo, CategoryInfo>(
            $"SELECT {TopPrefix(dialect, 3)}{JoinSql}{LimitSuffix(dialect, 3)}", options);

        Assert.Equal(3, results.Count);
        Assert.True(results.Capacity >= 500);
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    public async Task QueryAsync_TwoEntities_WithExpectedRowCount_PreSizesListCapacity(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        var options = CommandOptions<(ProductInfo, CategoryInfo)>.WithExpectedRowCount(500);

        var results = await connection.QueryAsync<ProductInfo, CategoryInfo>(
            $"SELECT {TopPrefix(dialect, 3)}{JoinSql}{LimitSuffix(dialect, 3)}", options, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.True(results.Capacity >= 500);
    }
}
