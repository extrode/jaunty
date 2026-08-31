using System.Data;
using System.Data.Common;

using Jaunty.Core;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Integration.Execute;

/// <summary>
/// AUD-R34-009. <c>ExecuteBatch</c> assigns <c>command.CommandText = sql</c> once, before the loop,
/// and <c>ParameterBinder.Bind</c> <b>rewrites</b> that text when a parameter set contains a
/// collection to expand into an IN clause (<c>ParameterBinder.cs</c>: <c>command.CommandText =
/// expandedSql</c>). So the second set in a batch was bound against the first set's expanded text -
/// <c>IN (@Ids0, @Ids1)</c> - which names parameters no property matches. The batch either throws
/// on the second set or executes it against the wrong placeholders; either way one set's shape
/// poisons every later set.
/// <para>
/// The same loop latches <c>prepared = true</c> forever, so <c>Prepare()</c> was called for the
/// first set's text and parameter shape and never again, even on the iterations that clear the
/// collection and rebind a different shape against different text.
/// </para>
/// </summary>
public class ExecuteBatchCollectionExpansionTests
{
    private const string DeleteSql = "DELETE FROM batch_expand WHERE id IN @Ids";

    private static SqliteConnection CreateSqlite()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE batch_expand (id INTEGER PRIMARY KEY);" +
            "INSERT INTO batch_expand (id) VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10);";
        create.ExecuteNonQuery();

        return connection;
    }

    private static List<long> RemainingIds(IDbConnection connection)
    {
        using IDbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM batch_expand ORDER BY id";
        using IDataReader reader = cmd.ExecuteReader();

        var ids = new List<long>();
        while (reader.Read()) ids.Add(reader.GetInt64(0));
        return ids;
    }

    [Fact]
    public void ExecuteBatch_CollectionSetsOfDifferentLengths_ExecutesEverySet()
    {
        using SqliteConnection connection = CreateSqlite();

        int total = connection.ExecuteBatch(DeleteSql, new object[]
        {
            new { Ids = new[] { 1, 2 } },
            new { Ids = new[] { 3, 4, 5 } }
        });

        Assert.Equal(5, total);
        Assert.Equal(new List<long> { 6, 7, 8, 9, 10 }, RemainingIds(connection));
    }

    [Fact]
    public void ExecuteBatch_CollectionSetsOfEqualLength_UsesEachSetsOwnValues()
    {
        using SqliteConnection connection = CreateSqlite();

        int total = connection.ExecuteBatch(DeleteSql, new object[]
        {
            new { Ids = new[] { 1, 2 } },
            new { Ids = new[] { 3, 4 } }
        });

        Assert.Equal(4, total);
        Assert.Equal(new List<long> { 5, 6, 7, 8, 9, 10 }, RemainingIds(connection));
    }

    [Fact]
    public async Task ExecuteBatchAsync_CollectionSetsOfDifferentLengths_ExecutesEverySet()
    {
        using SqliteConnection connection = CreateSqlite();

        int total = await connection.ExecuteBatchAsync(DeleteSql, new object[]
        {
            new { Ids = new[] { 1, 2 } },
            new { Ids = new[] { 3, 4, 5 } }
        });

        Assert.Equal(5, total);
        Assert.Equal(new List<long> { 6, 7, 8, 9, 10 }, RemainingIds(connection));
    }

    [Fact]
    public async Task ExecuteBatchAsync_CollectionSetsOfEqualLength_UsesEachSetsOwnValues()
    {
        using SqliteConnection connection = CreateSqlite();

        int total = await connection.ExecuteBatchAsync(DeleteSql, new object[]
        {
            new { Ids = new[] { 1, 2 } },
            new { Ids = new[] { 3, 4 } }
        });

        Assert.Equal(4, total);
        Assert.Equal(new List<long> { 5, 6, 7, 8, 9, 10 }, RemainingIds(connection));
    }

    [Fact]
    public void ExecuteBatch_SetsWithAStableShape_PreparesOnce()
    {
        using SqliteConnection sqlite = CreateSqlite();
        using var connection = new PrepareCountingConnection(sqlite);

        connection.ExecuteBatch("DELETE FROM batch_expand WHERE id = @Id", new object[]
        {
            new { Id = 1 },
            new { Id = 2 },
            new { Id = 3 }
        });

        Assert.Equal(1, connection.PrepareCount);
    }

    /// <summary>
    /// Each expanded set clears the parameter collection and rebinds a different shape against
    /// different text, so the plan prepared for the previous set is stale and must be prepared
    /// again - the latched flag skipped every prepare after the first.
    /// </summary>
    [Fact]
    public void ExecuteBatch_EachRebind_PreparesAgain()
    {
        using SqliteConnection sqlite = CreateSqlite();
        using var connection = new PrepareCountingConnection(sqlite);

        connection.ExecuteBatch(DeleteSql, new object[]
        {
            new { Ids = new[] { 1, 2 } },
            new { Ids = new[] { 3, 4, 5 } }
        });

        Assert.Equal(2, connection.PrepareCount);
    }

    /// <summary>
    /// Delegates to a real SQLite connection and counts <c>Prepare()</c> calls on the commands it
    /// hands out. <c>InnerConnection</c> is public because that is one of the property names
    /// <c>SqlDialectFactory</c> unwraps decorators by, so the dialect still resolves to SQLite.
    /// </summary>
    private sealed class PrepareCountingConnection : DbConnection
    {
        private readonly SqliteConnection _inner;

        public PrepareCountingConnection(SqliteConnection inner) => _inner = inner;

        public IDbConnection InnerConnection => _inner;

        public int PrepareCount { get; private set; }

#pragma warning disable CS8765
        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }
#pragma warning restore CS8765

        public override string Database => _inner.Database;
        public override string DataSource => _inner.DataSource;
        public override string ServerVersion => _inner.ServerVersion;
        public override ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public override void Close() => _inner.Close();
        public override void Open() => _inner.Open();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);

        protected override DbCommand CreateDbCommand() => new CountingCommand(_inner.CreateCommand(), this);

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }

        private sealed class CountingCommand : DbCommand
        {
            private readonly SqliteCommand _inner;
            private readonly PrepareCountingConnection _owner;

            public CountingCommand(SqliteCommand inner, PrepareCountingConnection owner)
            {
                _inner = inner;
                _owner = owner;
            }

#pragma warning disable CS8765
            public override string CommandText
            {
                get => _inner.CommandText;
                set => _inner.CommandText = value;
            }
#pragma warning restore CS8765

            public override int CommandTimeout
            {
                get => _inner.CommandTimeout;
                set => _inner.CommandTimeout = value;
            }

            public override CommandType CommandType
            {
                get => _inner.CommandType;
                set => _inner.CommandType = value;
            }

            public override bool DesignTimeVisible
            {
                get => _inner.DesignTimeVisible;
                set => _inner.DesignTimeVisible = value;
            }

            public override UpdateRowSource UpdatedRowSource
            {
                get => _inner.UpdatedRowSource;
                set => _inner.UpdatedRowSource = value;
            }

            protected override DbConnection? DbConnection
            {
                get => _owner;
                set { }
            }

            protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

            protected override DbTransaction? DbTransaction
            {
                get => _inner.Transaction;
                set => _inner.Transaction = (SqliteTransaction?)value;
            }

            public override void Cancel() => _inner.Cancel();
            public override int ExecuteNonQuery() => _inner.ExecuteNonQuery();
            public override object? ExecuteScalar() => _inner.ExecuteScalar();

            public override void Prepare()
            {
                _owner.PrepareCount++;
                _inner.Prepare();
            }

            protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => _inner.ExecuteReader(behavior);

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
