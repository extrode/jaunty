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
/// holds the DELETE off until both statements finish.
/// </remarks>
internal static class UpsertOwnTransaction
{
    /// <summary>
    /// Begins the transaction, or returns <see langword="null"/> when the provider refuses one.
    /// MySqlConnector, Microsoft.Data.Sqlite and DuckDB.NET throw <see cref="InvalidOperationException"/>
    /// when the connection already has a transaction; the command then runs as it did before, and the
    /// provider reports a transaction the caller opened but did not pass in the usual way.
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
}
