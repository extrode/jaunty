using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Inner = System.Data.SQLite;

namespace Extrode.Jaunty.Fluent.Tests.Helpers.AsyncDisposal;

/// <summary>
/// Wraps a System.Data.SQLite connection under the same type name, so the SQLite dialect still
/// resolves, and records whether commands, readers and the connection were released through their
/// async or their blocking members.
/// </summary>
internal sealed class SQLiteConnection(string databasePath) : DbConnection
{
    private readonly Inner.SQLiteConnection _inner = new($"Data Source={databasePath}");

    public List<string> Released { get; } = new();

    [AllowNull]
    public override string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value ?? ""; }
    public override string Database => _inner.Database;
    public override string DataSource => _inner.DataSource;
    public override string ServerVersion => _inner.ServerVersion;
    public override ConnectionState State => _inner.State;

    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

    public override void Open() => _inner.Open();

    public override void Close()
    {
        Released.Add("connection.Close");
        _inner.Close();
    }

    public override Task CloseAsync()
    {
        Released.Add("connection.CloseAsync");
        _inner.Close();
        return Task.CompletedTask;
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);

    protected override DbCommand CreateDbCommand() => new ProbeCommand(this, _inner.CreateCommand());

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class ProbeCommand(SQLiteConnection connection, Inner.SQLiteCommand inner) : DbCommand
{
    private bool _released;

    [AllowNull]
    public override string CommandText { get => inner.CommandText; set => inner.CommandText = value ?? ""; }
    public override int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
    public override CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
    public override bool DesignTimeVisible { get => inner.DesignTimeVisible; set => inner.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource { get => inner.UpdatedRowSource; set => inner.UpdatedRowSource = value; }
    protected override DbConnection? DbConnection { get => connection; set { } }
    protected override DbParameterCollection DbParameterCollection => inner.Parameters;
    protected override DbTransaction? DbTransaction { get => inner.Transaction; set => inner.Transaction = (Inner.SQLiteTransaction?)value; }

    public override void Cancel() => inner.Cancel();
    public override int ExecuteNonQuery() => inner.ExecuteNonQuery();
    public override object? ExecuteScalar() => inner.ExecuteScalar();
    public override void Prepare() => inner.Prepare();
    protected override DbParameter CreateDbParameter() => inner.CreateParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new ProbeReader(connection, inner.ExecuteReader(behavior));

    public override ValueTask DisposeAsync()
    {
        if (!_released)
        {
            _released = true;
            connection.Released.Add("command.DisposeAsync");
            inner.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_released)
        {
            _released = true;
            connection.Released.Add("command.Dispose");
            inner.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class ProbeReader(SQLiteConnection connection, DbDataReader inner) : DbDataReader
{
    private bool _released;

    public override object this[int ordinal] => inner[ordinal];
    public override object this[string name] => inner[name];
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override string GetString(int ordinal) => inner.GetString(ordinal);
    public override object GetValue(int ordinal) => inner.GetValue(ordinal);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
    public override bool NextResult() => inner.NextResult();
    public override bool Read() => inner.Read();
    public override IEnumerator GetEnumerator() => ((IEnumerable)inner).GetEnumerator();

    public override ValueTask DisposeAsync()
    {
        if (!_released)
        {
            _released = true;
            connection.Released.Add("reader.DisposeAsync");
            inner.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_released)
        {
            _released = true;
            connection.Released.Add("reader.Dispose");
            inner.Dispose();
        }
        base.Dispose(disposing);
    }
}
