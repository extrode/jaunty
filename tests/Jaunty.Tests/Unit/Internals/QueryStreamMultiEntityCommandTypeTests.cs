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
        _realConnection = new SQLiteConnection(NorthwindDatabase.ConnectionString);
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

#region Minimal In-Memory Fakes For Direct CommandType Observation

/// <summary>
/// A minimal <see cref="IDbConnection"/> that deliberately does NOT extend
/// <see cref="System.Data.Common.DbConnection"/>, forcing execution through the
/// <c>QueryStreamMultiEntityCore</c> fallback branch. <see cref="CreateCommand"/> always returns
/// the same <see cref="SpyCommand"/> instance so tests can inspect the <see cref="CommandType"/>
/// that Jaunty applied to it.
/// </summary>
internal sealed class SpyConnection : IDbConnection
{
    public string ConnectionString { get; set; } = "";
    public int ConnectionTimeout => 30;
    public string Database => "spy";
    public ConnectionState State { get; private set; } = ConnectionState.Closed;

    public SpyCommand? LastCommand { get; private set; }

    public IDbTransaction BeginTransaction() => throw new NotImplementedException();
    public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
    public void ChangeDatabase(string databaseName) => throw new NotImplementedException();
    public void Close() => State = ConnectionState.Closed;
    public IDbCommand CreateCommand() => LastCommand = new SpyCommand();
    public void Open() => State = ConnectionState.Open;
    public void Dispose() { }
}

/// <summary>
/// A minimal <see cref="IDbCommand"/> whose <see cref="ExecuteReader()"/> always returns an
/// <see cref="EmptyDataReader"/>, so tests can observe the <see cref="CommandType"/> Jaunty sets
/// without needing to model any actual rows.
/// </summary>
internal sealed class SpyCommand : IDbCommand
{
    public string CommandText { get; set; } = "";
    public int CommandTimeout { get; set; }
    public CommandType CommandType { get; set; }
    public IDbConnection? Connection { get; set; }
    public IDataParameterCollection Parameters => throw new NotImplementedException();
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() { }
    public IDbDataParameter CreateParameter() => throw new NotImplementedException();
    public void Dispose() { }
    public int ExecuteNonQuery() => 0;
    public IDataReader ExecuteReader() => new EmptyDataReader();
    public IDataReader ExecuteReader(CommandBehavior behavior) => new EmptyDataReader();
    public object? ExecuteScalar() => null;
    public void Prepare() { }
}

/// <summary>
/// A minimal <see cref="IDbTransaction"/> that is deliberately not a
/// <see cref="System.Data.Common.DbTransaction"/>, so tests can prove Jaunty assigns it to
/// <see cref="IDbCommand.Transaction"/> rather than silently dropping it.
/// </summary>
internal sealed class FakeTransaction : IDbTransaction
{
    public IDbConnection? Connection => null;
    public IsolationLevel IsolationLevel => IsolationLevel.Unspecified;
    public void Commit() { }
    public void Rollback() { }
    public void Dispose() { }
}

/// <summary>
/// An <see cref="IDataReader"/> with zero rows. <c>QueryStreamMultiEntityCore</c>'s
/// <c>if (!reader.Read()) yield break;</c> short-circuits on this before any column mapping is
/// attempted, so the fake never needs to implement column access.
/// </summary>
internal sealed class EmptyDataReader : IDataReader
{
    public object this[int i] => throw new NotImplementedException();
    public object this[string name] => throw new NotImplementedException();
    public int Depth => 0;
    public bool IsClosed { get; private set; }
    public int RecordsAffected => 0;
    public int FieldCount => 0;

    public void Close() => IsClosed = true;
    public void Dispose() => IsClosed = true;
    public bool GetBoolean(int i) => throw new NotImplementedException();
    public byte GetByte(int i) => throw new NotImplementedException();
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();
    public char GetChar(int i) => throw new NotImplementedException();
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();
    public IDataReader GetData(int i) => throw new NotImplementedException();
    public string GetDataTypeName(int i) => throw new NotImplementedException();
    public DateTime GetDateTime(int i) => throw new NotImplementedException();
    public decimal GetDecimal(int i) => throw new NotImplementedException();
    public double GetDouble(int i) => throw new NotImplementedException();
    public Type GetFieldType(int i) => throw new NotImplementedException();
    public float GetFloat(int i) => throw new NotImplementedException();
    public Guid GetGuid(int i) => throw new NotImplementedException();
    public short GetInt16(int i) => throw new NotImplementedException();
    public int GetInt32(int i) => throw new NotImplementedException();
    public long GetInt64(int i) => throw new NotImplementedException();
    public string GetName(int i) => throw new NotImplementedException();
    public int GetOrdinal(string name) => throw new NotImplementedException();
    public DataTable? GetSchemaTable() => null;
    public string GetString(int i) => throw new NotImplementedException();
    public object GetValue(int i) => throw new NotImplementedException();
    public int GetValues(object[] values) => 0;
    public bool IsDBNull(int i) => throw new NotImplementedException();
    public bool NextResult() => false;
    public bool Read() => false;
}

#endregion
