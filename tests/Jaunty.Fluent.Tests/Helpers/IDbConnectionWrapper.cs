using System.Data;

namespace Jaunty.Fluent.Tests.Helpers;

/// <summary>
/// Wraps a real IDbConnection without extending DbConnection, so an
/// <c>is DbConnection</c> check fails. Used to verify async code paths reject a
/// non-DbConnection with a clear NotSupportedException instead of silently falling back
/// to a blocking synchronous call.
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
    public IDbCommand CreateCommand() => _inner.CreateCommand();
    public void Open() => _inner.Open();
    public void Dispose() => _inner.Dispose();
}
