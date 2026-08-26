using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Entities;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// Regression tests for a bug in <c>QueryCore.cs</c> where the private
/// <c>QueryStreamMultiEntityCore</c>/<c>QueryStreamMultiEntityCoreFast</c> methods (backing the
/// public <c>QueryStream&lt;T1, T2&gt;</c> ... <c>QueryStream&lt;T1..T7&gt;</c> overloads that accept
/// <see cref="CommandOptions{T}"/>) never applied <c>options.CommandType</c> to the underlying
/// <see cref="IDbCommand"/>. Tests 1 and 2 use a real SQLite connection to prove the fix's new
/// conditional assignment (<c>if (options.CommandType is CommandType.StoredProcedure or
/// CommandType.TableDirect) command.CommandType = options.CommandType;</c>) is a safe no-op for the
/// default <see cref="CommandType.Text"/> case on both the <see cref="System.Data.Common.DbConnection"/>
/// fast path and the <see cref="IDbConnection"/>-only fallback path. SQLite has no server-side
/// stored procedures, so it cannot prove that <see cref="CommandType.StoredProcedure"/> is actually
/// propagated; Test 3 proves that directly with a minimal in-memory fake <see cref="IDbConnection"/>
/// that returns an empty reader, letting us inspect the <see cref="IDbCommand.CommandType"/> that
/// Jaunty actually set without needing a real multi-row data source.
/// </summary>
public class QueryStreamMultiEntityCommandTypeTests : IDisposable
{
    private readonly SQLiteConnection _realConnection;
    private readonly IDbConnectionWrapper _wrapper;

    public QueryStreamMultiEntityCommandTypeTests()
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
    public void QueryStream_FastPath_WithTextCommandType_ReturnsCorrectResults()
    {
        var options = new CommandOptions<(OrderSummary, CategorySummary)>(commandType: CommandType.Text);

        var results = _realConnection.QueryStream<OrderSummary, CategorySummary>(JoinSql, options).ToList();

        Assert.Equal(3, results.Count);
        Assert.All(results, r =>
        {
            Assert.True(r.Item1.OrderId > 0);
            Assert.Equal(1, r.Item2.CategoryId);
            Assert.NotNull(r.Item2.CategoryName);
        });
    }

    [Fact]
    public void QueryStream_FallbackPath_WithTextCommandType_ReturnsCorrectResults()
    {
        var options = new CommandOptions<(OrderSummary, CategorySummary)>(commandType: CommandType.Text);

        var results = _wrapper.QueryStream<OrderSummary, CategorySummary>(JoinSql, options).ToList();

        Assert.Equal(3, results.Count);
        Assert.All(results, r =>
        {
            Assert.True(r.Item1.OrderId > 0);
            Assert.Equal(1, r.Item2.CategoryId);
            Assert.NotNull(r.Item2.CategoryName);
        });
    }

    [Fact]
    public void QueryStream_FallbackPath_WithStoredProcedureCommandType_SetsCommandTypeOnUnderlyingCommand()
    {
        var connection = new SpyConnection();
        var options = CommandOptions<(OrderSummary, CategorySummary)>.AsStoredProcedure();

        var results = connection.QueryStream<OrderSummary, CategorySummary>("dbo.GetOrdersWithCategory", options).ToList();

        Assert.Empty(results);
        Assert.NotNull(connection.LastCommand);
        Assert.Equal(CommandType.StoredProcedure, connection.LastCommand!.CommandType);
    }

    // AUD-R12 originally covered the obsolete QueryStream<T1, T2>(sql, parameters, CommandOptions)
    // overload, whose QueryStreamCoreIterator only assigned command.Transaction when
    // options.Transaction was already a System.Data.Common.DbTransaction, silently dropping any
    // other IDbTransaction implementation. That overload and its iterator were deleted with the
    // rest of the obsolete multi-entity surface; the same input now reaches the supported path,
    // because the AUD-R34-002 implicit CommandOptions -> CommandOptions<T> conversion binds a
    // non-generic CommandOptions to the CommandOptions<(T1, T2)> overload rather than boxing it
    // into object. The behaviour under test is unchanged: a non-DbTransaction must be assigned.
    [Fact]
    public void QueryStream_FallbackPath_WithNonDbTransaction_AssignsTransactionInsteadOfSilentlyDroppingIt()
    {
        var connection = new SpyConnection();
        var transaction = new FakeTransaction();

        var results = connection.QueryStream<OrderSummary, CategorySummary>(
            "dbo.GetOrdersWithCategory",
            options: new CommandOptions(transaction: transaction)).ToList();

        Assert.Empty(results);
        Assert.NotNull(connection.LastCommand);
        Assert.Same(transaction, connection.LastCommand!.Transaction);
    }
}
