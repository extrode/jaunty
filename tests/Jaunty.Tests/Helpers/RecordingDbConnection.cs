using System.Data;
using System.Data.Common;

namespace Jaunty.Tests.Helpers;

/// <summary>
/// A <see cref="DbConnection"/> wrapper that records what was set on each command at the moment it
/// executed, so a test can assert that <c>CommandOptions</c> actually reached the command rather
/// than inferring it from a result that would look the same either way.
/// <para>
/// The obvious alternative - open a transaction on a SQLite connection, write a sentinel through it
/// and assert the sentinel comes back - proves nothing, because SQLite scopes a transaction to the
/// connection: the sentinel is visible to every command on that connection whether or not
/// <c>command.Transaction</c> was ever assigned.
/// </para>
/// <para>
/// The values have to be captured during the call: Jaunty disposes the command before returning and
/// <c>SQLiteCommand</c> throws <see cref="ObjectDisposedException"/> from its getters afterwards.
/// <see cref="NonGenericCommandOptionsOverloadTests"/> has a private version of this shape;
/// this one is shared, records <see cref="CommandType"/> as well, and works on the async path
/// because it is a real <see cref="DbConnection"/>.
/// </para>
/// </summary>
public sealed class RecordingDbConnection : DbConnection
{
    private readonly DbConnection _inner;

    public RecordingDbConnection(DbConnection inner) => _inner = inner;

    /// <summary>The <see cref="IDbCommand.CommandTimeout"/> in force when a command executed.</summary>
    public int? ExecutedTimeout { get; private set; }

    /// <summary>The <see cref="IDbCommand.Transaction"/> in force when a command executed.</summary>
    public IDbTransaction? ExecutedTransaction { get; private set; }

    /// <summary>The <see cref="IDbCommand.CommandType"/> in force when a command executed.</summary>
    public CommandType? ExecutedCommandType { get; private set; }

    /// <summary>How many commands have executed, so a test can tell "not set" from "never ran".</summary>
    public int ExecutedCount { get; private set; }

    /// <summary>The most recently created command, for tests that assert before disposal.</summary>
    public RecordingDbCommand? LastCommand { get; private set; }

    /// <summary>
    /// Every command that executed, in order, as (text, timeout). AUD-R35-131 needed to assert what
    /// reached one particular command in a sequence rather than the last one.
    /// </summary>
    public List<(string CommandText, int CommandTimeout)> Executed { get; } = [];

    internal void Record(RecordingDbCommand command)
    {
        Executed.Add((command.CommandText, command.CommandTimeout));
        ExecutedTimeout = command.CommandTimeout;
        ExecutedTransaction = command.Transaction;
        ExecutedCommandType = command.AppliedCommandType;
        ExecutedCount++;
    }

#pragma warning disable CS8765 // base DbConnection.ConnectionString setter is [AllowNull]; the polyfill for that attribute is file-scoped in the netstandard2.0 SDK.
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

    protected override DbCommand CreateDbCommand() => LastCommand = new RecordingDbCommand(_inner.CreateCommand(), this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}

/// <summary>
/// The command half of <see cref="RecordingDbConnection"/>. Everything forwards to a real command
/// except <see cref="CommandType"/>, which is recorded but not propagated - SQLite rejects
/// <see cref="CommandType.StoredProcedure"/>, and the point is to observe what Jaunty asked for.
/// </summary>
public sealed class RecordingDbCommand : DbCommand
{
    private readonly DbCommand _inner;
    private readonly RecordingDbConnection _owner;

    public RecordingDbCommand(DbCommand inner, RecordingDbConnection owner)
    {
        _inner = inner;
        _owner = owner;
    }

    /// <summary>What the caller assigned to <see cref="CommandType"/>, unfiltered by SQLite.</summary>
    public CommandType AppliedCommandType { get; private set; } = CommandType.Text;

#pragma warning disable CS8765 // base DbCommand.CommandText setter is [AllowNull]; see the note on ConnectionString above.
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
        get => AppliedCommandType;
        set => AppliedCommandType = value;
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
        set { /* fixed to the owning wrapper */ }
    }

    protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _inner.Transaction;
        set => _inner.Transaction = value;
    }

    public override void Cancel() => _inner.Cancel();
    protected override DbParameter CreateDbParameter() => _inner.CreateParameter();
    public override void Prepare() { }

    public override int ExecuteNonQuery()
    {
        _owner.Record(this);
        return _inner.ExecuteNonQuery();
    }

    public override object? ExecuteScalar()
    {
        _owner.Record(this);
        return _inner.ExecuteScalar();
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        _owner.Record(this);
        return _inner.ExecuteNonQueryAsync(cancellationToken);
    }

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        _owner.Record(this);
        return _inner.ExecuteScalarAsync(cancellationToken);
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        _owner.Record(this);
        return _inner.ExecuteReader(behavior);
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        _owner.Record(this);
        return _inner.ExecuteReaderAsync(behavior, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}
