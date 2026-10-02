using System.Data;
using System.Data.Common;

namespace Extrode.Jaunty.Internals.Write;

/// <summary>
/// The transaction Upsert begins around its UPDATE-then-INSERT pair for a database-generated key on
/// a dialect where each statement would otherwise commit on its own
/// (<see cref="CachedCrudSql.UpsertNeedsOwnTransaction"/>).
/// </summary>
/// <remarks>
/// Without it, a DELETE of the row landing between the two statements lets the INSERT's
/// <c>NOT EXISTS</c> guard pass, so the deleted row comes back under a new key and the call reports
/// 2 rows. Inside a transaction the UPDATE's lock (InnoDB) or the single write lock (SQLite, DuckDB)
/// holds the DELETE off until both statements finish. DuckDB's optimistic concurrency fails one of the
/// two with a conflict error instead of making the DELETE wait; either way the row is not resurrected.
/// </remarks>
internal static class UpsertOwnTransaction
{
    /// <summary>
    /// Begins the transaction, or returns <see langword="null"/> when the provider refuses one.
    /// MySqlConnector, Microsoft.Data.Sqlite and DuckDB.NET throw <see cref="InvalidOperationException"/>
    /// when the connection already has a transaction; the command then runs as it did before, inside
    /// that transaction where the provider allows it. Any other refusal is treated the same way: the
    /// connection is open at this point, so an <see cref="InvalidOperationException"/> from
    /// <c>BeginTransaction</c> means the connection is already in a transaction or cannot start one,
    /// and the command's own execution then reports anything that is really wrong.
    /// System.Data.SQLite does not refuse: it returns a nested transaction (see
    /// <see cref="CommitAfterFailure"/>).
    /// </summary>
    internal static IDbTransaction? TryBegin(IDbConnection connection)
    {
        try
        {
            return connection.BeginTransaction();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <inheritdoc cref="TryBegin"/>
    internal static async ValueTask<DbTransaction?> TryBeginAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
#if NET8_0_OR_GREATER
            return await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
#else
            await Task.CompletedTask.ConfigureAwait(false);
            return connection.BeginTransaction();
#endif
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    internal static async ValueTask CommitAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
#else
        await Task.CompletedTask.ConfigureAwait(false);
        transaction.Commit();
#endif
    }

    /// <summary>
    /// Ends the transaction after a statement failed by committing it, not rolling it back.
    /// </summary>
    /// <remarks>
    /// There is never anything to undo: each statement is atomic on its own, and the INSERT's
    /// <c>NOT EXISTS</c> guard only lets it write when the UPDATE matched no row. Rolling back would be
    /// harmful on System.Data.SQLite, whose <c>BeginTransaction</c> inside a transaction the caller
    /// opened but did not pass returns a nested level whose rollback ends the caller's whole
    /// transaction; a commit only leaves the level. Best effort: the original exception is the one the
    /// caller sees, and a commit that fails leaves <c>Dispose</c> to roll back.
    /// </remarks>
    internal static void CommitAfterFailure(IDbTransaction? transaction)
    {
        if (transaction is null)
            return;

        try
        {
            transaction.Commit();
        }
        catch
        {
            // Best effort, see remarks.
        }
    }

    /// <inheritdoc cref="CommitAfterFailure"/>
    internal static async ValueTask CommitAfterFailureAsync(DbTransaction? transaction)
    {
        if (transaction is null)
            return;

        try
        {
            await CommitAsync(transaction, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Best effort, see remarks.
        }
    }
}
