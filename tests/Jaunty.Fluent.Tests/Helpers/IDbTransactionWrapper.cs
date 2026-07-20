using System.Data;

namespace Jaunty.Fluent.Tests.Helpers;

/// <summary>
/// Wraps a real IDbTransaction without extending DbTransaction, so an
/// <c>is DbTransaction</c>/<c>AsyncTransactionValidator.RequireDbTransaction</c> check fails.
/// Used to verify code paths reject a non-DbTransaction with a clear ArgumentException instead
/// of an opaque InvalidCastException.
/// </summary>
public class IDbTransactionWrapper : IDbTransaction
{
    private readonly IDbTransaction _inner;

    public IDbTransactionWrapper(IDbTransaction inner)
    {
        _inner = inner;
    }

    public IDbConnection? Connection => _inner.Connection;
    public IsolationLevel IsolationLevel => _inner.IsolationLevel;

    public void Commit() => _inner.Commit();
    public void Rollback() => _inner.Rollback();
    public void Dispose() => _inner.Dispose();
}
