using System.Data.SQLite;

using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.IDbConnectionFallback;

/// <summary>
/// Tests that the async multi-entity overloads correctly reject non-DbConnection
/// wrappers instead of letting an unchecked cast to DbDataReader throw a misleading
/// InvalidCastException. The async public API requires DbConnection and throws
/// InvalidOperationException for IDbConnection-only implementations.
/// </summary>
public class AsyncMultiEntityFallbackTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    public AsyncMultiEntityFallbackTests()
    {
        _realConnection = new SQLiteConnection("Data Source=../../../../../data/sqlite/Northwind.db");
        _wrapper = new IDbConnectionWrapper(_realConnection);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _realConnection.Dispose();
    }

    private const string JoinSql =
        "SELECT o.order_id AS OrderId, o.customer_id AS CustomerId, c.category_id AS CategoryId, c.category_name AS CategoryName FROM orders o, categories c WHERE c.category_id = 1 ORDER BY o.order_id LIMIT 3";

    [Fact]
    public async Task QueryAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        // Passing CancellationToken.None explicitly disambiguates between the
        // (connection, sql, cancellationToken) and (connection, sql, parameters, options,
        // cancellationToken) overloads, both of which are satisfiable with just (connection, sql).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.QueryAsync<OrderSummary, CategorySummary>(JoinSql, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task QueryStreamAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach ((OrderSummary, CategorySummary) _ in _wrapper.QueryStreamAsync<OrderSummary, CategorySummary>(JoinSql))
            {
            }
        });
    }

    [Fact]
    public async Task QueryFirstAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.QueryFirstAsync<OrderSummary, CategorySummary>(JoinSql, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task QueryFirstOrDefaultAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.QueryFirstOrDefaultAsync<OrderSummary, CategorySummary>(JoinSql, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task QuerySingleAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.QuerySingleAsync<OrderSummary, CategorySummary>(JoinSql, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task QuerySingleOrDefaultAsync_ViaWrapper_ThrowsForNonDbConnection()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _wrapper.QuerySingleOrDefaultAsync<OrderSummary, CategorySummary>(JoinSql, CancellationToken.None).AsTask());
    }
}
