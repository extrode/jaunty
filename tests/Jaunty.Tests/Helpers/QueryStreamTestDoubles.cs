using Jaunty.Core;

// Namespace preserved from QueryStreamMultiEntityCommandTypeTests.cs so every existing
// reference still resolves. These doubles were defined at the bottom of that file, which moved
// to the parallel Jaunty.UnitTests assembly - but Integration/Read/QueryPartialListTests.cs
// stayed behind in the serial Jaunty.Tests and uses SpyConnection. Both projects share this
// file by Compile link, so there is one definition rather than a copy per assembly.
namespace Jaunty.Tests.Unit.Internals;

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
