using System.Data;
using System.Data.Common;

using Jaunty.Dialects;

namespace Jaunty.Internals.Write;

/// <summary>
/// Coordinates the disable/enable of foreign key enforcement around Bulk* write operations.
/// Some providers (notably SQLite, where <c>PRAGMA foreign_keys = OFF/ON</c> is a documented
/// no-op while a transaction is pending) can only toggle constraints in autocommit mode - i.e.
/// outside any active transaction. This helper centralizes both that autocommit-mode toggling
/// and the classic in-transaction toggling used by providers like MySQL and PostgreSQL, and
/// guards against the impossible combination of a caller-supplied transaction with a provider
/// that requires autocommit.
/// </summary>
internal static class ForeignKeyToggleCoordinator
{
    internal static void ValidateTransactionCompatibility(bool ignoreConstraints, ISqlDialect dialect, IDbTransaction? callerTransaction, string providerName)
    {
        if (ignoreConstraints && dialect.RequiresAutocommitForForeignKeyToggle && callerTransaction is not null)
            throw new NotSupportedException(
                $"The database provider ({providerName}) can only toggle foreign key constraints outside an active transaction (autocommit mode), " +
                "so ignoreConstraints cannot be combined with a caller-supplied transaction. " +
                "Call the method without supplying a transaction so Jaunty can toggle constraints itself before and after its own transaction, " +
                "or disable foreign key checks manually before starting your own transaction.");
    }

    internal static bool RequiresPreTransactionToggle(bool ignoreConstraints, ISqlDialect dialect)
        => ignoreConstraints && dialect.RequiresAutocommitForForeignKeyToggle;

    internal static void DisableSync(IDbConnection connection, ISqlDialect dialect, IDbTransaction? transaction)
    {
        using IDbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = RequireToggleSql(dialect.GetDisableForeignKeyChecksSql(), dialect, nameof(ISqlDialect.GetDisableForeignKeyChecksSql));
        command.ExecuteNonQuery();
    }

    internal static void EnableSync(IDbConnection connection, ISqlDialect dialect, IDbTransaction? transaction)
    {
        using IDbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = RequireToggleSql(dialect.GetEnableForeignKeyChecksSql(), dialect, nameof(ISqlDialect.GetEnableForeignKeyChecksSql));
        command.ExecuteNonQuery();
    }

    internal static async Task DisableAsync(DbConnection connection, ISqlDialect dialect, DbTransaction? transaction, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        DbCommand command = connection.CreateCommand();
        await using var commandDisposer = command.ConfigureAwait(false);
#else
        using DbCommand command = connection.CreateCommand();
#endif
        command.Transaction = transaction;
        command.CommandText = RequireToggleSql(dialect.GetDisableForeignKeyChecksSql(), dialect, nameof(ISqlDialect.GetDisableForeignKeyChecksSql));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task EnableAsync(DbConnection connection, ISqlDialect dialect, DbTransaction? transaction, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        DbCommand command = connection.CreateCommand();
        await using var commandDisposer = command.ConfigureAwait(false);
#else
        using DbCommand command = connection.CreateCommand();
#endif
        command.Transaction = transaction;
        command.CommandText = RequireToggleSql(dialect.GetEnableForeignKeyChecksSql(), dialect, nameof(ISqlDialect.GetEnableForeignKeyChecksSql));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns a dialect that claims <see cref="ISqlDialect.SupportsForeignKeyToggle"/> but supplies
    /// no toggle SQL into an error that names it.
    /// </summary>
    /// <remarks>
    /// AUD-R26. These four methods assigned <c>dialect.GetDisableForeignKeyChecksSql()!</c> straight
    /// to <c>CommandText</c>, and the null-forgiving operator was load-bearing:
    /// <c>SqlServerDialect</c> genuinely returns null here (SqlServerDialect.cs:217). The only thing
    /// keeping a null command text away from the provider is that every caller separately checks
    /// <c>SupportsForeignKeyToggle</c> first (e.g. BulkInsert.cs 158) - two independent members of a
    /// public interface with no invariant tying them together. A third-party dialect whose answers
    /// disagree reached the provider with a null <c>CommandText</c> and failed there, naming neither
    /// the dialect nor the member that lied. Checking here costs one null test per bulk operation,
    /// not per row.
    /// </remarks>
    private static string RequireToggleSql(string? sql, ISqlDialect dialect, string member)
    {
        if (string.IsNullOrEmpty(sql))
            throw new InvalidOperationException(
                $"Dialect '{dialect.GetType().Name}' reports SupportsForeignKeyToggle = true but its " +
                $"{member}() returned no SQL. Those two members have to agree: either return the " +
                "statement that toggles foreign key checks, or report SupportsForeignKeyToggle = false " +
                "so callers take the path that does not need it.");

        return sql!;
    }
}
