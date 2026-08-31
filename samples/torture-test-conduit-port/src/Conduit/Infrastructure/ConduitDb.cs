using System.Data;

using Jaunty;
using Jaunty.Core;

namespace Conduit.Infrastructure;

/// <summary>
/// Jaunty-backed replacement for the former EF Core <c>ConduitContext</c>. Wraps a single
/// ADO.NET connection plus the ambient transaction every MediatR request runs inside
/// (see <see cref="ConduitDbTransactionPipelineBehavior{TRequest,TResponse}"/>), so handlers
/// can issue Jaunty fluent queries/writes against <see cref="Connection"/> without each one
/// having to thread a transaction through manually.
/// </summary>
public class ConduitDb
{
    private IDbTransaction? _currentTransaction;

    public IDbConnection Connection { get; }

    public ConduitDb(IDbConnection connection)
    {
        Connection = connection;

        if (Connection.State != ConnectionState.Open)
        {
            Connection.Open();
        }
    }

    /// <summary>
    /// The <see cref="CommandOptions"/> to pass to every Jaunty write call, carrying the
    /// ambient transaction (if one is active) so writes within a single request are atomic.
    /// </summary>
    public CommandOptions Options =>
        _currentTransaction is not null
            ? CommandOptions.WithTransaction(_currentTransaction)
            : default;

    public void EnsureCreated() => Connection.Execute(ConduitSchema.CreateTablesSql);

    #region Transaction Handling
    public void BeginTransaction()
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = Connection.BeginTransaction(IsolationLevel.ReadCommitted);
    }

    public void CommitTransaction()
    {
        try
        {
            _currentTransaction?.Commit();
        }
        catch
        {
            RollbackTransaction();
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public void RollbackTransaction()
    {
        try
        {
            _currentTransaction?.Rollback();
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }
    #endregion
}
