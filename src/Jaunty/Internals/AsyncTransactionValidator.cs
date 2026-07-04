using System.Data;
using System.Data.Common;

namespace Jaunty;

internal static class AsyncTransactionValidator
{
    /// <summary>
    /// Casts <paramref name="transaction"/> to <see cref="DbTransaction"/> for use on an async execution path.
    /// </summary>
    /// <remarks>
    /// Async commands are built via <see cref="DbCommand"/>, whose <see cref="DbCommand.Transaction"/> setter
    /// only accepts <see cref="DbTransaction"/>. If a caller passes an <see cref="IDbTransaction"/> that isn't
    /// a <see cref="DbTransaction"/>, it must not be silently ignored (which would run the command outside the
    /// caller's transaction, or cause bulk operations to start their own separate transaction) - so this throws
    /// instead of returning null for a non-null, incompatible transaction.
    /// </remarks>
    internal static DbTransaction? RequireDbTransaction(IDbTransaction? transaction)
    {
        if (transaction is null)
            return null;

        if (transaction is DbTransaction dbTransaction)
            return dbTransaction;

        throw new ArgumentException(
            $"The provided transaction of type '{transaction.GetType().Name}' does not derive from " +
            $"'{nameof(DbTransaction)}'. Async Jaunty operations require a {nameof(DbTransaction)} " +
            "(e.g. obtained via DbConnection.BeginTransactionAsync) so it can be attached to the " +
            "underlying async command.",
            nameof(transaction));
    }
}
