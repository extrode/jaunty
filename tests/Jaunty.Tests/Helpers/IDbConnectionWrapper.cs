using System.Data;

namespace Jaunty.Tests.Helpers;

/// <summary>
/// Wraps a real IDbConnection without extending DbConnection.
/// When passed to Jaunty methods, the <c>if (connection is DbConnection)</c> check fails,
/// forcing execution through the IDbConnection fallback code paths.
/// </summary>
public class IDbConnectionWrapper : IDbConnection
{
    private readonly IDbConnection _inner;

    public IDbConnectionWrapper(IDbConnection inner)
    {
        _inner = inner;
    }

    public string ConnectionString
    {
        get => _inner.ConnectionString;
        set => _inner.ConnectionString = value;
    }

    public int ConnectionTimeout => _inner.ConnectionTimeout;
    public string Database => _inner.Database;
    public ConnectionState State => _inner.State;

    public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
    public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
    public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
    public void Close() => _inner.Close();
    public IDbCommand CreateCommand() => new IDbCommandWrapper(_inner.CreateCommand());
    public void Open() => _inner.Open();
    public void Dispose() => _inner.Dispose();
}

/// <summary>
/// Wraps a real IDbCommand without extending DbCommand.
/// Returns IDataReaderWrapper from ExecuteReader to ensure DbDataReader checks also fail.
/// </summary>
public class IDbCommandWrapper : IDbCommand
{
    private readonly IDbCommand _inner;

    public IDbCommandWrapper(IDbCommand inner)
    {
        _inner = inner;
    }

    public string CommandText
    {
        get => _inner.CommandText;
        set => _inner.CommandText = value;
    }

    public int CommandTimeout
    {
        get => _inner.CommandTimeout;
        set => _inner.CommandTimeout = value;
    }

    public CommandType CommandType
    {
        get => _inner.CommandType;
        set => _inner.CommandType = value;
    }

    public IDbConnection? Connection
    {
        get => _inner.Connection;
        set => _inner.Connection = value;
    }

    public IDataParameterCollection Parameters => _inner.Parameters;

    public IDbTransaction? Transaction
    {
        get => _inner.Transaction;
        set => _inner.Transaction = value;
    }

    public UpdateRowSource UpdatedRowSource
    {
        get => _inner.UpdatedRowSource;
        set => _inner.UpdatedRowSource = value;
    }

    public void Cancel() => _inner.Cancel();
    public IDbDataParameter CreateParameter() => _inner.CreateParameter();
    public int ExecuteNonQuery() => _inner.ExecuteNonQuery();

    public IDataReader ExecuteReader() => new IDataReaderWrapper(_inner.ExecuteReader());
    public IDataReader ExecuteReader(CommandBehavior behavior) => new IDataReaderWrapper(_inner.ExecuteReader(behavior));
    public object? ExecuteScalar() => _inner.ExecuteScalar();
    public void Prepare() => _inner.Prepare();
    public void Dispose() => _inner.Dispose();
}

/// <summary>
/// Wraps a real IDataReader without extending DbDataReader.
/// When checked via <c>if (reader is DbDataReader)</c>, this fails,
/// forcing the IDataReader fallback paths (Convert.ChangeType, sync Read, etc.).
/// </summary>
public class IDataReaderWrapper : IDataReader
{
    private readonly IDataReader _inner;

    public IDataReaderWrapper(IDataReader inner)
    {
        _inner = inner;
    }

    public object this[int i] => _inner[i];
    public object this[string name] => _inner[name];

    public int Depth => _inner.Depth;
    public bool IsClosed => _inner.IsClosed;
    public int RecordsAffected => _inner.RecordsAffected;
    public int FieldCount => _inner.FieldCount;

    public void Close() => _inner.Close();
    public void Dispose() => _inner.Dispose();
    public bool GetBoolean(int i) => _inner.GetBoolean(i);
    public byte GetByte(int i) => _inner.GetByte(i);
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => _inner.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
    public char GetChar(int i) => _inner.GetChar(i);
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => _inner.GetChars(i, fieldoffset, buffer, bufferoffset, length);
    public IDataReader GetData(int i) => _inner.GetData(i);
    public string GetDataTypeName(int i) => _inner.GetDataTypeName(i);
    public DateTime GetDateTime(int i) => _inner.GetDateTime(i);
    public decimal GetDecimal(int i) => _inner.GetDecimal(i);
    public double GetDouble(int i) => _inner.GetDouble(i);
    public Type GetFieldType(int i) => _inner.GetFieldType(i);
    public float GetFloat(int i) => _inner.GetFloat(i);
    public Guid GetGuid(int i) => _inner.GetGuid(i);
    public short GetInt16(int i) => _inner.GetInt16(i);
    public int GetInt32(int i) => _inner.GetInt32(i);
    public long GetInt64(int i) => _inner.GetInt64(i);
    public string GetName(int i) => _inner.GetName(i);
    public int GetOrdinal(string name) => _inner.GetOrdinal(name);
    public DataTable? GetSchemaTable() => _inner.GetSchemaTable();
    public string GetString(int i) => _inner.GetString(i);
    public object GetValue(int i) => _inner.GetValue(i);
    public int GetValues(object[] values) => _inner.GetValues(values);
    public bool IsDBNull(int i) => _inner.IsDBNull(i);
    public bool NextResult() => _inner.NextResult();
    public bool Read() => _inner.Read();
}