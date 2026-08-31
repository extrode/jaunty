using System.Data.Common;
using System.Runtime.CompilerServices;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Dialects;
using Jaunty.FlatFiles.Interfaces;
using Jaunty.Internals;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Handles VIEW→TABLE promotion for flat file sources on first mutation.
/// Uses transactions to ensure atomic promotion (CREATE TABLE, DROP VIEW, RENAME).
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26-069. Promotion used to be recorded solely as <see cref="IFileSource.IsPromotedToTable"/>
/// - state on the <em>file source</em>, describing something that happened on <em>one connection</em>.
/// <c>FlatFile.Open(string)</c> builds a fresh source per call, so this was invisible there; but
/// <c>FlatFile.Open(Action&lt;FlatFileOptions&gt;)</c> takes whatever <see cref="IFileSource"/>
/// instances the caller adds, and nothing stops the same instance being added to two
/// <c>FlatFileOptions</c> - which is the natural way to write "the same CSV, two databases". The
/// first <c>DuckDb</c> promoted the view to a table and set the flag; the second saw the flag already
/// true, skipped promotion, and then issued an UPDATE or DELETE against a <b>view</b>, which DuckDB
/// rejects.
/// </para>
/// <para>
/// The authority is now the per-connection set below, so each connection promotes its own view
/// exactly once. <see cref="IFileSource.IsPromotedToTable"/> is still set - it is public API, and
/// useful as "this source has been promoted somewhere" - but it is no longer what the decision reads.
/// </para>
/// <para>
/// The check-then-act was also unguarded, and <c>GeneratePromoteToTableSql</c> emits
/// <c>CREATE TABLE x_tmp AS ...; DROP VIEW x; ALTER TABLE x_tmp RENAME TO x;</c> with no
/// <c>IF NOT EXISTS</c> on any of the three - so two threads mutating the same source both observed
/// the flag as false, both ran it, and the loser failed on <c>x_tmp</c> already existing. The
/// per-connection gate below is held across the whole sequence, which serialises them.
/// </para>
/// </remarks>
internal static class TablePromoter
{
    /// <summary>
    /// Per-connection promotion state. Keyed weakly so it dies with the connection rather than
    /// pinning it, and so a long-lived process opening many connections does not accumulate entries.
    /// </summary>
    private static readonly ConditionalWeakTable<DuckDBConnection, ConnectionState> States = new();

    private sealed class ConnectionState
    {
        /// <summary>Serialises the check-promote-record sequence; a plain lock cannot span the await
        /// in the async path.</summary>
        public readonly SemaphoreSlim Gate = new(1, 1);

        /// <summary>Sources already promoted on this connection. Guarded by <see cref="Gate"/>.</summary>
        public readonly HashSet<IFileSource> Promoted = new();
    }

    /// <summary>
    /// Promotes a VIEW source to a TABLE on first mutation. No-op if already a table on this
    /// connection, or preloaded.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="source">The file source to promote.</param>
    /// <param name="dialect">The DuckDB dialect for SQL generation.</param>
    public static void EnsurePromotedToTable(DuckDBConnection connection, IFileSource source, DuckDbDialect dialect)
    {
        if (PreloadRegistry.IsPreloaded(connection, source))
            return;

        ConnectionState state = States.GetValue(connection, static _ => new ConnectionState());

        state.Gate.Wait();
        try
        {
            if (state.Promoted.Contains(source))
                return;

            var sql = dialect.GeneratePromoteToTableSql(source);

            CommandObservation.Execute(sql, null, connection, DuckDbObservation.Text, () =>
            {
                PromoteDirect(connection, sql);
                return true;
            });

            state.Promoted.Add(source);
            source.IsPromotedToTable = true;
        }
        finally
        {
            state.Gate.Release();
        }
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
    /// Promotes a VIEW source to a TABLE on first mutation asynchronously. No-op if already a table
    /// on this connection, or preloaded.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="source">The file source to promote.</param>
    /// <param name="dialect">The DuckDB dialect for SQL generation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    public static async ValueTask EnsurePromotedToTableAsync(DuckDBConnection connection, IFileSource source, DuckDbDialect dialect, CancellationToken cancellationToken)
    {
        if (PreloadRegistry.IsPreloaded(connection, source))
            return;

        ConnectionState state = States.GetValue(connection, static _ => new ConnectionState());

        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state.Promoted.Contains(source))
                return;

            var sql = dialect.GeneratePromoteToTableSql(source);

            await CommandObservation.ExecuteAsync(sql, null, connection, DuckDbObservation.Text,
                async () => { await PromoteDirectAsync(connection, sql, cancellationToken).ConfigureAwait(false); return true; },
                cancellationToken).ConfigureAwait(false);

            state.Promoted.Add(source);
            source.IsPromotedToTable = true;
        }
        finally
        {
            state.Gate.Release();
        }
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
