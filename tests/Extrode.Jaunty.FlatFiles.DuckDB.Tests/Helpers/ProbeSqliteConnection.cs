using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;

internal sealed class CountingSynchronizationContext : SynchronizationContext
{
    private readonly int[] _shared;

    public CountingSynchronizationContext() => _shared = new int[1];

    private CountingSynchronizationContext(int[] shared) => _shared = shared;

    public int Posts => Volatile.Read(ref _shared[0]);

    public CountingSynchronizationContext Fork() => new(_shared);

    public override void Post(SendOrPostCallback d, object? state)
    {
        Interlocked.Increment(ref _shared[0]);
        ThreadPool.QueueUserWorkItem(_ => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        Interlocked.Increment(ref _shared[0]);
        d(state);
    }
}

internal sealed class ProbeSqliteConnection : DbConnection
{
    private readonly SqliteConnection _inner = new("DataSource=:memory:");

    public ProbeSqliteConnection(bool canBatch = true, CountingSynchronizationContext? context = null)
    {
        CanBatch = canBatch;
        Context = context;
    }

    public bool CanBatch { get; }

    public CountingSynchronizationContext? Context { get; }

    public SqliteConnection Inner => _inner;

    public List<int> BatchSizes { get; } = new();

    public List<string> ExecutedTexts { get; } = new();

    public List<string[]> BatchParameterNames { get; } = new();

    public int PrepareCalls { get; set; }

    public Func<string, bool>? FailReaderWhen { get; set; }

    public Func<string, bool>? FailNonQueryWhen { get; set; }

    [AllowNull]
    public override string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }

    public override string Database => _inner.Database;

    public override string DataSource => _inner.DataSource;

    public override string ServerVersion => _inner.ServerVersion;

    public override ConnectionState State => _inner.State;

    public override bool CanCreateBatch => CanBatch;

    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

    public override void Close() => _inner.Close();

    public override void Open() => _inner.Open();

    public override Task OpenAsync(CancellationToken cancellationToken) => Later(() => { _inner.Open(); return true; });

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        new ProbeTransaction(this, _inner.BeginTransaction(isolationLevel));

    protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) =>
        new(Later(() => (DbTransaction)new ProbeTransaction(this, _inner.BeginTransaction(isolationLevel))));

    protected override DbCommand CreateDbCommand() => new ProbeCommand(this, _inner.CreateCommand());

    protected override DbBatch CreateDbBatch() => new ProbeBatch(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    public override ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return default;
    }

    internal Task<T> Later<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.None);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(Context?.Fork());
            try
            {
                completion.SetResult(work());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        });
        return completion.Task;
    }

    internal Task Later(Action work) => Later(() => { work(); return true; });

    internal ValueTask LaterValue(Action work) => new(Later(work));
}

internal sealed class ProbeTransaction : DbTransaction
{
    private readonly ProbeSqliteConnection _connection;
    private readonly SqliteTransaction _inner;

    public ProbeTransaction(ProbeSqliteConnection connection, SqliteTransaction inner)
    {
        _connection = connection;
        _inner = inner;
    }

    public SqliteTransaction Inner => _inner;

    public override IsolationLevel IsolationLevel => _inner.IsolationLevel;

    protected override DbConnection DbConnection => _connection;

    public override void Commit() => _inner.Commit();

    public override void Rollback() => _inner.Rollback();

    public override Task CommitAsync(CancellationToken cancellationToken = default) => _connection.Later(() => _inner.Commit());

    public override Task RollbackAsync(CancellationToken cancellationToken = default) => _connection.Later(() => _inner.Rollback());

    public override ValueTask DisposeAsync() => _connection.LaterValue(() => _inner.Dispose());

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class ProbeCommand : DbCommand
{
    private readonly ProbeSqliteConnection _connection;
    private readonly SqliteCommand _inner;
    private DbTransaction? _transaction;

    public ProbeCommand(ProbeSqliteConnection connection, SqliteCommand inner)
    {
        _connection = connection;
        _inner = inner;
        _inner.Connection = connection.Inner;
    }

    [AllowNull]
    public override string CommandText { get => _inner.CommandText; set => _inner.CommandText = value; }

    public override int CommandTimeout { get => _inner.CommandTimeout; set => _inner.CommandTimeout = value; }

    public override CommandType CommandType { get => _inner.CommandType; set => _inner.CommandType = value; }

    public override bool DesignTimeVisible { get => _inner.DesignTimeVisible; set => _inner.DesignTimeVisible = value; }

    public override UpdateRowSource UpdatedRowSource { get => _inner.UpdatedRowSource; set => _inner.UpdatedRowSource = value; }

    protected override DbConnection? DbConnection { get => _connection; set { } }

    protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

    protected override DbTransaction? DbTransaction
    {
        get => _transaction;
        set
        {
            _transaction = value;
            _inner.Transaction = (value as ProbeTransaction)?.Inner ?? (SqliteTransaction?)value;
        }
    }

    public override void Cancel() => _inner.Cancel();

    public override void Prepare()
    {
        _connection.PrepareCalls++;
        _inner.Prepare();
    }

    protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

    public override int ExecuteNonQuery() => Run(() => _inner.ExecuteNonQuery());

    public override object? ExecuteScalar() => Run(() => _inner.ExecuteScalar());

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        Guard(_connection.FailReaderWhen);
        return new ProbeReader(_connection, _inner.ExecuteReader(behavior));
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => _connection.Later(ExecuteNonQuery);

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => _connection.Later(ExecuteScalar);

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
        _connection.Later(() => ExecuteDbDataReader(behavior));

    public override ValueTask DisposeAsync() => _connection.LaterValue(() => _inner.Dispose());

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    private T Run<T>(Func<T> execute)
    {
        Guard(_connection.FailNonQueryWhen);
        _connection.ExecutedTexts.Add(CommandText);
        return execute();
    }

    private void Guard(Func<string, bool>? fail)
    {
        if (fail is not null && fail(CommandText))
            throw new SqliteException("probe failure", 1);
    }
}

internal sealed class ProbeReader : DbDataReader
{
    private readonly ProbeSqliteConnection _connection;
    private readonly SqliteDataReader _inner;

    public ProbeReader(ProbeSqliteConnection connection, SqliteDataReader inner)
    {
        _connection = connection;
        _inner = inner;
    }

    public override int Depth => _inner.Depth;

    public override int FieldCount => _inner.FieldCount;

    public override bool HasRows => _inner.HasRows;

    public override bool IsClosed => _inner.IsClosed;

    public override int RecordsAffected => _inner.RecordsAffected;

    public override object this[int ordinal] => _inner[ordinal];

    public override object this[string name] => _inner[name];

    public override bool GetBoolean(int ordinal) => _inner.GetBoolean(ordinal);

    public override byte GetByte(int ordinal) => _inner.GetByte(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);

    public override char GetChar(int ordinal) => _inner.GetChar(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);

    public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);

    public override DateTime GetDateTime(int ordinal) => _inner.GetDateTime(ordinal);

    public override decimal GetDecimal(int ordinal) => _inner.GetDecimal(ordinal);

    public override double GetDouble(int ordinal) => _inner.GetDouble(ordinal);

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);

    public override float GetFloat(int ordinal) => _inner.GetFloat(ordinal);

    public override Guid GetGuid(int ordinal) => _inner.GetGuid(ordinal);

    public override short GetInt16(int ordinal) => _inner.GetInt16(ordinal);

    public override int GetInt32(int ordinal) => _inner.GetInt32(ordinal);

    public override long GetInt64(int ordinal) => _inner.GetInt64(ordinal);

    public override string GetName(int ordinal) => _inner.GetName(ordinal);

    public override int GetOrdinal(string name) => _inner.GetOrdinal(name);

    public override string GetString(int ordinal) => _inner.GetString(ordinal);

    public override object GetValue(int ordinal) => _inner.GetValue(ordinal);

    public override int GetValues(object[] values) => _inner.GetValues(values);

    public override bool IsDBNull(int ordinal) => _inner.IsDBNull(ordinal);

    public override bool NextResult() => _inner.NextResult();

    public override bool Read() => _inner.Read();

    public override IEnumerator GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => _connection.Later(() => _inner.Read());

    public override ValueTask DisposeAsync() => _connection.LaterValue(() => _inner.Dispose());

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class ProbeBatch : DbBatch
{
    private readonly ProbeSqliteConnection _connection;
    private readonly ProbeBatchCommandCollection _commands = new();

    public ProbeBatch(ProbeSqliteConnection connection) => _connection = connection;

    protected override DbBatchCommandCollection DbBatchCommands => _commands;

    public override int Timeout { get; set; }

    protected override DbConnection? DbConnection { get => _connection; set { } }

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }

    public override void Prepare() { }

    public override Task PrepareAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    protected override DbBatchCommand CreateDbBatchCommand() => new ProbeBatchCommand();

    public override int ExecuteNonQuery()
    {
        _connection.BatchSizes.Add(_commands.Count);
        int total = 0;
        foreach (ProbeBatchCommand command in _commands.Items)
        {
            using SqliteCommand sqlite = _connection.Inner.CreateCommand();
            sqlite.CommandText = command.CommandText;
            sqlite.Transaction = (DbTransaction as ProbeTransaction)?.Inner;
            foreach (SqliteParameter parameter in command.SqliteParameters)
                sqlite.Parameters.Add(new SqliteParameter(parameter.ParameterName, parameter.Value));
            _connection.ExecutedTexts.Add(command.CommandText);
            _connection.BatchParameterNames.Add(command.SqliteParameters.Select(p => p.ParameterName).ToArray());
            total += sqlite.ExecuteNonQuery();
        }

        return total;
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default) =>
        _connection.Later(ExecuteNonQuery);

    public override object? ExecuteScalar() => throw new NotSupportedException();

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public override ValueTask DisposeAsync() => _connection.LaterValue(() => { });

    public override void Dispose() { }
}

internal sealed class ProbeBatchCommand : DbBatchCommand
{
    private readonly SqliteCommand _parameters = new();

    [AllowNull]
    public override string CommandText { get; set; } = "";

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override int RecordsAffected => 0;

    public IEnumerable<SqliteParameter> SqliteParameters => _parameters.Parameters.Cast<SqliteParameter>();

    protected override DbParameterCollection DbParameterCollection => _parameters.Parameters;

    public override bool CanCreateParameter => true;

    public override DbParameter CreateParameter() => _parameters.CreateParameter();
}

internal sealed class ProbeBatchCommandCollection : DbBatchCommandCollection
{
    private readonly List<DbBatchCommand> _items = new();

    public IEnumerable<ProbeBatchCommand> Items => _items.Cast<ProbeBatchCommand>();

    public override int Count => _items.Count;

    public override bool IsReadOnly => false;

    public override void Add(DbBatchCommand item) => _items.Add(item);

    public override void Clear() => _items.Clear();

    public override bool Contains(DbBatchCommand item) => _items.Contains(item);

    public override void CopyTo(DbBatchCommand[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public override IEnumerator<DbBatchCommand> GetEnumerator() => _items.GetEnumerator();

    public override int IndexOf(DbBatchCommand item) => _items.IndexOf(item);

    public override void Insert(int index, DbBatchCommand item) => _items.Insert(index, item);

    public override bool Remove(DbBatchCommand item) => _items.Remove(item);

    public override void RemoveAt(int index) => _items.RemoveAt(index);

    protected override DbBatchCommand GetBatchCommand(int index) => _items[index];

    protected override void SetBatchCommand(int index, DbBatchCommand batchCommand) => _items[index] = batchCommand;
}
