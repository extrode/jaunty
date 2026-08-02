using System.Data.SQLite;

using Jaunty.Core;
using Jaunty.Tests.Helpers;

namespace Jaunty.Tests.Integration.Read;

/// <summary>
/// AUD-R34-002. The entity-mapping APIs pair an <c>(IDbConnection, string, object parameters)</c>
/// overload with an <c>(IDbConnection, string, CommandOptions&lt;T&gt;)</c> one. The only conversion
/// on <see cref="CommandOptions{T}"/> used to be generic -> non-generic, so a non-generic
/// <see cref="CommandOptions"/> - what <c>CommandOptions.WithTransaction</c> and
/// <c>CommandOptions.WithTimeout</c> return - was not convertible to <c>CommandOptions&lt;T&gt;</c>
/// and bound to <c>object parameters</c> instead.
/// <para>
/// Nothing complained. <c>ParameterBinder.Bind</c> treated the struct as a parameters object,
/// <c>IsScalarType</c> is false for it, and <c>CommandOptions</c> exposes public <i>fields</i> rather
/// than properties, so <c>ParameterCache.Get</c> yielded no metadata and the "unused parameter
/// properties" guard had nothing to report. Zero parameters bound, no error was raised, and the
/// transaction or timeout the caller asked for was discarded.
/// </para>
/// <para>
/// The remedy is the reverse implicit conversion on <see cref="CommandOptions{T}"/>, not an overload
/// per API: overload resolution prefers a conversion to <c>CommandOptions&lt;T&gt;</c> over boxing to
/// <c>object</c>, so every arity of every entity API is fixed by the one operator. These tests assert
/// the binding, not just that rows came back - a call that returns rows is exactly what the defect
/// looked like.
/// </para>
/// </summary>
public class NonGenericCommandOptionsOverloadTests
{
    internal sealed class Row
    {
        public long Id { get; set; }
    }

    /// <summary>
    /// Records what was set on the command at the moment it executed. The shared
    /// <see cref="IDbConnectionWrapper"/> exposes <c>LastCommand</c>, but Jaunty disposes the command
    /// before returning, and <c>SQLiteCommand</c> throws <see cref="ObjectDisposedException"/> from
    /// its property getters afterwards - so the values have to be taken during the call.
    /// </summary>
    private sealed class RecordingConnection : IDbConnection
    {
        private readonly IDbConnection _inner;

        internal RecordingConnection(IDbConnection inner) => _inner = inner;

        internal int? ExecutedTimeout { get; private set; }
        internal IDbTransaction? ExecutedTransaction { get; private set; }

        public string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
        public int ConnectionTimeout => _inner.ConnectionTimeout;
        public string Database => _inner.Database;
        public ConnectionState State => _inner.State;

        public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
        public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public void Close() => _inner.Close();
        public void Open() => _inner.Open();
        public void Dispose() => _inner.Dispose();

        public IDbCommand CreateCommand() => new RecordingCommand(_inner.CreateCommand(), this);

        private void Record(IDbCommand command)
        {
            ExecutedTimeout = command.CommandTimeout;
            ExecutedTransaction = command.Transaction;
        }

        private sealed class RecordingCommand : IDbCommand
        {
            private readonly IDbCommand _inner;
            private readonly RecordingConnection _owner;

            internal RecordingCommand(IDbCommand inner, RecordingConnection owner)
            {
                _inner = inner;
                _owner = owner;
            }

            public string CommandText { get => _inner.CommandText; set => _inner.CommandText = value; }
            public int CommandTimeout { get => _inner.CommandTimeout; set => _inner.CommandTimeout = value; }
            public CommandType CommandType { get => _inner.CommandType; set => _inner.CommandType = value; }
            public IDbConnection? Connection { get => _inner.Connection; set => _inner.Connection = value; }
            public IDataParameterCollection Parameters => _inner.Parameters;
            public IDbTransaction? Transaction { get => _inner.Transaction; set => _inner.Transaction = value; }
            public UpdateRowSource UpdatedRowSource { get => _inner.UpdatedRowSource; set => _inner.UpdatedRowSource = value; }

            public void Cancel() => _inner.Cancel();
            public IDbDataParameter CreateParameter() => _inner.CreateParameter();
            public void Prepare() => _inner.Prepare();
            public void Dispose() => _inner.Dispose();

            public int ExecuteNonQuery()
            {
                _owner.Record(_inner);
                return _inner.ExecuteNonQuery();
            }

            public object? ExecuteScalar()
            {
                _owner.Record(_inner);
                return _inner.ExecuteScalar();
            }

            public IDataReader ExecuteReader()
            {
                _owner.Record(_inner);
                return new IDataReaderWrapper(_inner.ExecuteReader());
            }

            public IDataReader ExecuteReader(CommandBehavior behavior)
            {
                _owner.Record(_inner);
                return new IDataReaderWrapper(_inner.ExecuteReader(behavior));
            }
        }
    }

    private static RecordingConnection CreateAndSeed()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE ngopt (id INTEGER PRIMARY KEY); INSERT INTO ngopt VALUES (1);";
            cmd.ExecuteNonQuery();
        }
        return new RecordingConnection(connection);
    }

    /// <summary>
    /// The damaging case: the caller asks for the work to run inside their transaction. Before the
    /// fix the command was issued with no transaction attached at all.
    /// </summary>
    [Fact]
    public void Query_GivenANonGenericCommandOptionsWithATransaction_AppliesTheTransaction()
    {
        using RecordingConnection connection = CreateAndSeed();
        using IDbTransaction transaction = connection.BeginTransaction();

        List<Row> rows = connection.Query<Row>("SELECT id AS Id FROM ngopt", CommandOptions.WithTransaction(transaction));

        Assert.Single(rows);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }

    [Fact]
    public void Query_GivenANonGenericCommandOptionsWithATimeout_AppliesTheTimeout()
    {
        using RecordingConnection connection = CreateAndSeed();

        List<Row> rows = connection.Query<Row>("SELECT id AS Id FROM ngopt", CommandOptions.WithTimeout(97));

        Assert.Single(rows);
        Assert.Equal(97, connection.ExecutedTimeout);
    }

    /// <summary>
    /// All three fields the non-generic form carries have to survive the conversion, not just the two
    /// the defect was reported against. Asserted on the conversion rather than through a query because
    /// SQLite rejects <see cref="CommandType.StoredProcedure"/> before the command executes, so no
    /// value would be observable at the connection.
    /// </summary>
    [Fact]
    public void TheConversionToTheGenericForm_CarriesEveryFieldTheNonGenericFormHas()
    {
        using RecordingConnection connection = CreateAndSeed();
        using IDbTransaction transaction = connection.BeginTransaction();

        CommandOptions<Row> converted = new CommandOptions(transaction, commandTimeout: 97, commandType: CommandType.StoredProcedure);

        Assert.Same(transaction, converted.Transaction);
        Assert.Equal(97, converted.CommandTimeout);
        Assert.Equal(CommandType.StoredProcedure, converted.CommandType);
        Assert.Null(converted.Mapper);
        Assert.Null(converted.ExpectedRowCount);
    }

    /// <summary>
    /// The four-argument shape, which did not compile at all before the conversion existed: there was
    /// no <c>(sql, parameters, CommandOptions)</c> overload and no conversion to the generic one.
    /// </summary>
    [Fact]
    public void Query_GivenParametersAndANonGenericCommandOptions_AppliesBoth()
    {
        using RecordingConnection connection = CreateAndSeed();

        List<Row> rows = connection.Query<Row>(
            "SELECT id AS Id FROM ngopt WHERE id = @id", new { id = 1 }, CommandOptions.WithTimeout(97));

        Assert.Single(rows);
        Assert.Equal(97, connection.ExecutedTimeout);
    }

    /// <summary>
    /// The conversion must not steal the <c>object parameters</c> overload from its actual callers.
    /// An anonymous type is still a parameters object and must still bind, or the fix would have
    /// broken every parameterised query in the library.
    /// </summary>
    [Fact]
    public void Query_GivenAnAnonymousParametersObject_StillBindsItAsParameters()
    {
        using RecordingConnection connection = CreateAndSeed();

        List<Row> present = connection.Query<Row>("SELECT id AS Id FROM ngopt WHERE id = @id", new { id = 1 });
        List<Row> absent = connection.Query<Row>("SELECT id AS Id FROM ngopt WHERE id = @id", new { id = 2 });

        Assert.Single(present);
        Assert.Empty(absent);
    }

    /// <summary>
    /// The generic form is the one that works, and is what every entity-mapping call site should use.
    /// Without this control the two tests above would also pass if the options plumbing were broken
    /// outright rather than the overload merely being bypassed.
    /// </summary>
    [Fact]
    public void Query_GivenTheGenericCommandOptions_AppliesTheTimeout()
    {
        using RecordingConnection connection = CreateAndSeed();

        List<Row> rows = connection.Query("SELECT id AS Id FROM ngopt", CommandOptions<Row>.WithTimeout(97));

        Assert.Single(rows);
        Assert.Equal(97, connection.ExecutedTimeout);
    }

    [Fact]
    public void Query_GivenTheGenericCommandOptions_AppliesTheTransaction()
    {
        using RecordingConnection connection = CreateAndSeed();
        using IDbTransaction transaction = connection.BeginTransaction();

        List<Row> rows = connection.Query("SELECT id AS Id FROM ngopt", CommandOptions<Row>.WithTransaction(transaction));

        Assert.Single(rows);
        Assert.Same(transaction, connection.ExecutedTransaction);
    }
}
