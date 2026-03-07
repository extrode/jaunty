using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Dialects;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Handles VIEW→TABLE promotion for flat file sources on first mutation.
/// Uses transactions to ensure atomic promotion (CREATE TABLE, DROP VIEW, RENAME).
/// </summary>
internal static class TablePromoter
{
    /// <summary>
    /// Promotes a VIEW source to a TABLE on first mutation. No-op if already a table or preloaded.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="source">The file source to promote.</param>
    /// <param name="dialect">The DuckDB dialect for SQL generation.</param>
    public static void EnsurePromotedToTable(DuckDBConnection connection, IFileSource source, DuckDbDialect dialect)
    {
        if (source.IsPreloaded || source.IsPromotedToTable)
            return;

        var sql = dialect.GeneratePromoteToTableSql(source);

        using var transaction = connection.BeginTransaction();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = transaction;
            cmd.ExecuteNonQuery();
            transaction.Commit();
        }
        catch
        {
            try { transaction.Rollback(); } catch { }
            throw;
        }

        source.IsPromotedToTable = true;
    }

    /// <summary>
    /// Promotes a VIEW source to a TABLE on first mutation asynchronously. No-op if already a table or preloaded.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="source">The file source to promote.</param>
    /// <param name="dialect">The DuckDB dialect for SQL generation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    public static async ValueTask EnsurePromotedToTableAsync(DuckDBConnection connection, IFileSource source,         DuckDbDialect dialect, CancellationToken cancellationToken)
    {
        if (source.IsPreloaded || source.IsPromotedToTable)
            return;

        var sql = dialect.GeneratePromoteToTableSql(source);

        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = transaction;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        source.IsPromotedToTable = true;
    }
}
