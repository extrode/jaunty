using System.Data.Common;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Dialects;
using Jaunty.FlatFiles.Interfaces;
using Jaunty.Internals;

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

        CommandObservation.Execute(sql, null, connection, DuckDbObservation.Text, () =>
        {
            PromoteDirect(connection, sql);
            return true;
        });

        source.IsPromotedToTable = true;
    }

    private static void PromoteDirect(DuckDBConnection connection, string sql)
    {
        CommandObservation.Log(sql, null);

        using DuckDBTransaction transaction = connection.BeginTransaction();
        try
        {
            using DuckDBCommand cmd = connection.CreateCommand();
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
    }

    /// <summary>
    /// Promotes a VIEW source to a TABLE on first mutation asynchronously. No-op if already a table or preloaded.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="source">The file source to promote.</param>
    /// <param name="dialect">The DuckDB dialect for SQL generation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    public static async ValueTask EnsurePromotedToTableAsync(DuckDBConnection connection, IFileSource source, DuckDbDialect dialect, CancellationToken cancellationToken)
    {
        if (source.IsPreloaded || source.IsPromotedToTable)
            return;

        var sql = dialect.GeneratePromoteToTableSql(source);

        await CommandObservation.ExecuteAsync(sql, null, connection, DuckDbObservation.Text,
            async () => { await PromoteDirectAsync(connection, sql, cancellationToken).ConfigureAwait(false); return true; },
            cancellationToken).ConfigureAwait(false);

        source.IsPromotedToTable = true;
    }

    private static async ValueTask PromoteDirectAsync(DuckDBConnection connection, string sql, CancellationToken cancellationToken)
    {
        CommandObservation.Log(sql, null);

        DbTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            DuckDBCommand cmd = connection.CreateCommand();
            await using var cmdDisposer = cmd.ConfigureAwait(false);
            cmd.CommandText = sql;
            cmd.Transaction = transaction;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
