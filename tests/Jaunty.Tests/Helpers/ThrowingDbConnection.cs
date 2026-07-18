using System.Data;
using System.Data.Common;

namespace Jaunty.Tests.Helpers;

/// <summary>
/// A real <see cref="DbConnection"/> wrapper (delegating Open/Close/State to an inner connection)
/// whose commands can be configured, via <see cref="OnExecute"/>, to run a callback instead of
/// actually executing SQL. This lets regression tests deterministically cancel an external
/// <see cref="CancellationTokenSource"/> at the exact moment a command "completes" (successfully
/// or by throwing) -- immediately before production code's post-execution cleanup (e.g. closing a
/// connection or reader) runs -- without relying on timing-sensitive races.
/// </summary>
public class ThrowingDbConnection : DbConnection
{
    private readonly DbConnection _inner;

    public ThrowingDbConnection(DbConnection inner) => _inner = inner;

    /// <summary>
    /// Invoked by every command's ExecuteNonQueryAsync/ExecuteScalarAsync in place of real
    /// execution, when set. Receives the command's CancellationToken; its return value becomes
    /// the execute result, or it may throw to simulate a real (non-cancellation) failure.
    /// Reader execution (ExecuteReaderAsync) always delegates to the real inner command.
    /// </summary>
    public Func<CancellationToken, object?>? OnExecute { get; set; }

    /// <summary>
    /// The most recently created command wrapper, so tests can observe the reader it produced.
    /// </summary>
    public ThrowingDbCommand? LastCommand { get; private set; }

#pragma warning disable CS8765 // base DbConnection.ConnectionString setter is [AllowNull]; not using the attribute here since its netstandard2.0 SDK polyfill is file-scoped and inaccessible outside its own file.
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

    protected override DbCommand CreateDbCommand() => LastCommand = new ThrowingDbCommand(_inner.CreateCommand(), this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}

/// <summary>
/// Wraps a real <see cref="DbCommand"/>, forwarding everything to it except that
/// <see cref="CommandType"/> assignment is a no-op (so a caller setting
/// <see cref="CommandType.StoredProcedure"/> against a SQLite-backed inner command doesn't reject
/// it) and ExecuteNonQueryAsync/ExecuteScalarAsync are redirected through the owning
/// <see cref="ThrowingDbConnection.OnExecute"/> callback when one is configured.
/// </summary>
public sealed class ThrowingDbCommand : DbCommand
{
    private readonly DbCommand _inner;
    private readonly ThrowingDbConnection _owner;

    public ThrowingDbCommand(DbCommand inner, ThrowingDbConnection owner)
    {
        _inner = inner;
        _owner = owner;
    }

    /// <summary>
    /// The most recently created reader, so tests can assert whether cleanup closed it.
    /// </summary>
    public DbDataReader? LastReader { get; private set; }

#pragma warning disable CS8765 // base DbCommand.CommandText setter is [AllowNull]; not using the attribute here since its netstandard2.0 SDK polyfill is file-scoped and inaccessible outside its own file.
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
        set { /* SQLite has no real stored procedures; don't propagate to the inner command. */ }
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

    public override int ExecuteNonQuery() =>
        _owner.OnExecute is not null ? Convert.ToInt32(_owner.OnExecute(CancellationToken.None)) : _inner.ExecuteNonQuery();

    public override object? ExecuteScalar() =>
        _owner.OnExecute is not null ? _owner.OnExecute(CancellationToken.None) : _inner.ExecuteScalar();

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        return _owner.OnExecute is not null
            ? Task.FromResult(Convert.ToInt32(_owner.OnExecute(cancellationToken)))
            : _inner.ExecuteNonQueryAsync(cancellationToken);
    }

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        return _owner.OnExecute is not null
            ? Task.FromResult(_owner.OnExecute(cancellationToken))
            : _inner.ExecuteScalarAsync(cancellationToken);
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => LastReader = _inner.ExecuteReader(behavior);

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        return ExecuteReaderAsyncCore(behavior, cancellationToken);
    }

    private async Task<DbDataReader> ExecuteReaderAsyncCore(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        DbDataReader reader = await _inner.ExecuteReaderAsync(behavior, cancellationToken).ConfigureAwait(false);
        LastReader = reader;
        return reader;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }
}
