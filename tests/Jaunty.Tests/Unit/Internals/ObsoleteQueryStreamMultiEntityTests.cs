using System.Data;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Tests.Entities;

using Xunit;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R25 (B1-1): the obsolete
/// <c>QueryStream&lt;T1, T2&gt;(connection, sql, object? parameters, CommandOptions options)</c>
/// overload is backed by a hand-rolled <c>QueryStreamCoreIterator&lt;T1, T2&gt;</c> that applied
/// neither <c>options.CommandType</c> nor <c>JauntyConfig.Logger</c>.
///
/// <para>
/// Passing <c>CommandOptions.AsStoredProcedure()</c> through it therefore left the command as
/// <see cref="CommandType.Text"/>, so the provider executed the procedure <em>name</em> as a raw SQL
/// statement; and the command never reached the configured logger. Every other obsolete
/// multi-entity overload in the same file routes through <c>ExecuteReader</c>, which applies both,
/// and the non-obsolete replacement <c>QueryStreamMultiEntityCore</c> applies both as well - it was
/// fixed for exactly this CommandType gap and is pinned by
/// <see cref="QueryStreamMultiEntityCommandTypeTests"/>. This iterator had already been amended once
/// (AUD-R12, for a silently-dropped non-DbTransaction) without the sibling assignments being carried
/// across, and the gap contradicted the claim carried in all six <c>src/Jaunty/Streaming/*.cs</c>
/// files that streamed commands honor CommandType and the Logger callback.
/// </para>
/// </summary>
[Collection("Jaunty Config State")]
public class ObsoleteQueryStreamMultiEntityTests
{
    private const string Sql = "usp_GetOrdersAndCategories";

#pragma warning disable CS0618 // the overload under test is the obsolete one, deliberately

    [Theory]
    [InlineData(CommandType.StoredProcedure)]
    [InlineData(CommandType.TableDirect)]
    public void ObsoleteQueryStream_PropagatesCommandType(CommandType commandType)
    {
        var connection = new SpyConnection();
        var options = new CommandOptions(commandType: commandType);

        // The reader is empty, so enumerating drains the iterator without needing any rows.
        connection.QueryStream<OrderSummary, CategorySummary>(Sql, null, options).ToList();

        Assert.NotNull(connection.LastCommand);
        Assert.Equal(commandType, connection.LastCommand!.CommandType);
    }

    [Fact]
    public void ObsoleteQueryStream_LeavesTextCommandTypeAlone()
    {
        // The assignment is conditional, matching QueryStreamMultiEntityCore: Text must be left to
        // the provider's own default rather than explicitly re-set. SpyCommand initialises
        // CommandType to default(CommandType) (0, not Text), which is exactly what makes "was it
        // assigned?" observable here.
        var connection = new SpyConnection();
        var options = new CommandOptions(commandType: CommandType.Text);

        connection.QueryStream<OrderSummary, CategorySummary>(Sql, null, options).ToList();

        Assert.Equal(default, connection.LastCommand!.CommandType);
    }

    [Fact]
    public void ObsoleteQueryStream_InvokesTheConfiguredLogger()
    {
        var logged = new List<string>();
        Action<string, object?>? original = JauntyConfig.Logger;

        try
        {
            JauntyConfig.Logger = (sql, _) => logged.Add(sql);

            var connection = new SpyConnection();
            connection.QueryStream<OrderSummary, CategorySummary>(Sql, null, new CommandOptions()).ToList();

            Assert.Contains(Sql, logged);
        }
        finally
        {
            JauntyConfig.Logger = original;
        }
    }

    [Fact]
    public void ObsoleteQueryStream_LogsBeforeExecuting()
    {
        // Logging after ExecuteReader would mean a command that throws is never logged - the whole
        // point of the callback for diagnosing bad SQL. The sibling cores log before executing.
        var logged = new List<string>();
        Action<string, object?>? original = JauntyConfig.Logger;

        try
        {
            JauntyConfig.Logger = (sql, _) => logged.Add(sql);

            var connection = new ThrowingSpyConnection();

            Assert.ThrowsAny<Exception>(() =>
                connection.QueryStream<OrderSummary, CategorySummary>(Sql, null, new CommandOptions()).ToList());

            Assert.Contains(Sql, logged);
        }
        finally
        {
            JauntyConfig.Logger = original;
        }
    }

#pragma warning restore CS0618

    private sealed class ThrowingSpyConnection : IDbConnection
    {
        public string ConnectionString { get; set; } = "";
        public int ConnectionTimeout => 0;
        public string Database => "spy";
        public ConnectionState State { get; private set; } = ConnectionState.Closed;

        public IDbTransaction BeginTransaction() => throw new NotSupportedException();
        public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() => State = ConnectionState.Closed;
        public IDbCommand CreateCommand() => new ThrowingCommand { Connection = this };
        public void Open() => State = ConnectionState.Open;
        public void Dispose() { }

        private sealed class ThrowingCommand : IDbCommand
        {
            public string CommandText { get; set; } = "";
            public int CommandTimeout { get; set; }
            public CommandType CommandType { get; set; }
            public IDbConnection? Connection { get; set; }
            public IDataParameterCollection Parameters => throw new NotSupportedException();
            public IDbTransaction? Transaction { get; set; }
            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel() { }
            public IDbDataParameter CreateParameter() => throw new NotSupportedException();
            public void Dispose() { }
            public int ExecuteNonQuery() => 0;
            public IDataReader ExecuteReader() => throw new InvalidOperationException("bad sql");
            public IDataReader ExecuteReader(CommandBehavior behavior) => throw new InvalidOperationException("bad sql");
            public object? ExecuteScalar() => null;
            public void Prepare() { }
        }
    }
}
