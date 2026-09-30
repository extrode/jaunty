using System.Collections;
using System.Data;
using System.Data.Common;

namespace Extrode.Jaunty.Scaffolding.Tests.Helpers;

internal sealed class ScriptedConnection : DbConnection
{
    private readonly Func<ScriptedCommand, DataTable> _responder;
    private ConnectionState _state;
    private string _connectionString = string.Empty;

    public ScriptedConnection(
        Func<ScriptedCommand, DataTable> responder,
        ConnectionState state = ConnectionState.Closed,
        string database = "scripted-db",
        Func<int, bool>? completeAsync = null)
    {
        _responder = responder;
        _state = state;
        Database = database;
        _completeAsync = completeAsync;
    }

    private readonly Func<int, bool>? _completeAsync;

    public int AsyncPoints { get; private set; }

    internal async Task AsyncPoint()
    {
        int point = AsyncPoints++;
        if (_completeAsync?.Invoke(point) == true)
            await Task.Delay(1, CancellationToken.None).ConfigureAwait(false);
    }

    public List<ScriptedCommand> Commands { get; } = [];

    public int OpenCalls { get; private set; }

    public bool Disposed { get; private set; }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString
    {
        get => _connectionString;
        set => _connectionString = value ?? string.Empty;
    }

    public override string Database { get; }

    public override string DataSource => "scripted-source";

    public override string ServerVersion => "0";

    public override ConnectionState State => _state;

    internal DataTable Respond(ScriptedCommand command)
    {
        Commands.Add(command);
        return _responder(command);
    }

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close() => _state = ConnectionState.Closed;

    public override void Open()
    {
        OpenCalls++;
        _state = ConnectionState.Open;
    }

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        OpenCalls++;
        await AsyncPoint().ConfigureAwait(false);
        _state = ConnectionState.Open;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => new ScriptedCommand(this);

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}

internal sealed class ScriptedCommand : DbCommand
{
    private readonly ScriptedConnection _connection;
    private readonly ScriptedParameterCollection _parameters = new();

    public ScriptedCommand(ScriptedConnection connection) => _connection = connection;

    public IReadOnlyDictionary<string, object?> ParameterValues =>
        _parameters.Items.ToDictionary(p => p.ParameterName, p => p.Value is DBNull ? null : p.Value);

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; }

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => _connection;
        set => throw new NotSupportedException();
    }

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery() => throw new NotSupportedException();

    public override object? ExecuteScalar() => throw new NotSupportedException();

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new ScriptedParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new ScriptedReader(_connection.Respond(this), _connection);

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        await _connection.AsyncPoint().ConfigureAwait(false);
        return new ScriptedReader(_connection.Respond(this), _connection);
    }
}

internal sealed class ScriptedParameter : DbParameter
{
    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; }

    public override bool IsNullable { get; set; }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType()
    {
    }
}

internal sealed class ScriptedParameterCollection : DbParameterCollection
{
    public List<DbParameter> Items { get; } = [];

    public override int Count => Items.Count;

    public override object SyncRoot => ((ICollection)Items).SyncRoot;

    public override int Add(object value)
    {
        Items.Add((DbParameter)value);
        return Items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (object value in values)
            Add(value);
    }

    public override void Clear() => Items.Clear();

    public override bool Contains(object value) => Items.Contains((DbParameter)value);

    public override bool Contains(string value) => Items.Any(p => p.ParameterName == value);

    public override void CopyTo(Array array, int index) => ((ICollection)Items).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => Items.GetEnumerator();

    public override int IndexOf(object value) => Items.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) => Items.FindIndex(p => p.ParameterName == parameterName);

    public override void Insert(int index, object value) => Items.Insert(index, (DbParameter)value);

    public override void Remove(object value) => Items.Remove((DbParameter)value);

    public override void RemoveAt(int index) => Items.RemoveAt(index);

    public override void RemoveAt(string parameterName) => Items.RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => Items[index];

    protected override DbParameter GetParameter(string parameterName) => Items[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => Items[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) => Items[IndexOf(parameterName)] = value;
}

internal sealed class ScriptedReader : DbDataReader
{
    private readonly DataTableReader _inner;
    private readonly ScriptedConnection _connection;

    public ScriptedReader(DataTable table, ScriptedConnection connection)
    {
        _inner = table.CreateDataReader();
        _connection = connection;
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

    public override IEnumerator GetEnumerator() => ((IEnumerable)_inner).GetEnumerator();

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

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        await _connection.AsyncPoint().ConfigureAwait(false);
        return _inner.Read();
    }
}
