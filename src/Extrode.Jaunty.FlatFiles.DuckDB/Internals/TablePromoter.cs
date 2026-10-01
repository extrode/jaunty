using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;

using DuckDB.NET.Data;

using Extrode.Jaunty.FlatFiles.DuckDB.Dialects;
using Extrode.Jaunty.FlatFiles.Interfaces;
using Extrode.Jaunty.Internals;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Internals;

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
    /// <param name="transaction">The caller's transaction from <c>CommandOptions</c>, if any.</param>
    /// <remarks>
    /// AUD-R38-002: promotion used to open its own transaction unconditionally, and DuckDB.NET
    /// refuses a second transaction on a connection ("Already in a transaction."), so the first
    /// mutation of each source under a caller's transaction threw before it ran. Inside a caller's
    /// transaction - passed in <c>CommandOptions</c>, or begun on the connection and not passed - the
    /// promotion now joins it, and is not recorded: a rollback restores the view, so the next
    /// mutation must look again. It looks in the catalog (AUD-R38-017), which also covers a
    /// file-backed catalog reopened after an earlier instance's promotion persisted, where the
    /// unconditional <c>DROP VIEW</c> used to fail on the existing table.
    /// </remarks>
    public static void EnsurePromotedToTable(DuckDBConnection connection, IFileSource source, DuckDbDialect dialect, IDbTransaction? transaction = null)
    {
        if (PreloadRegistry.IsPreloaded(connection, source))
            return;

        ConnectionState state = States.GetValue(connection, static _ => new ConnectionState());

        state.Gate.Wait();
        try
        {
            if (state.Promoted.Contains(source))
                return;

            DbTransaction? callerTransaction = AsyncTransactionValidator.RequireDbTransaction(transaction);
            DuckDBTransaction? ownTransaction = callerTransaction is null ? TryBeginOwnTransaction(connection) : null;
            DbTransaction? active = callerTransaction ?? ownTransaction;
            try
            {
                if (!ExistsAsTable(connection, source.TableName, active))
                {
                    var sql = dialect.GeneratePromoteToTableSql(source);

                    CommandObservation.Execute(sql, null, connection, DuckDbObservation.Text, () =>
                    {
                        PromoteDirect(connection, sql, active);
                        return true;
                    });
                }

                if (ownTransaction is null)
                    return;

                ownTransaction.Commit();
            }
            catch
            {
                if (ownTransaction is not null)
                {
                    try { ownTransaction.Rollback(); } catch { }
                }
                throw;
            }
            finally
            {
                ownTransaction?.Dispose();
            }

            state.Promoted.Add(source);
            source.IsPromotedToTable = true;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    /// <summary>
    /// Begins the promotion's own transaction, or returns <see langword="null"/> when the connection
    /// already has one: DuckDB.NET throws <see cref="InvalidOperationException"/> for a second
    /// <c>BeginTransaction</c> and has no public way to ask whether one is open.
    /// </summary>
    private static DuckDBTransaction? TryBeginOwnTransaction(DuckDBConnection connection)
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

    private static bool ExistsAsTable(DuckDBConnection connection, string tableName, DbTransaction? transaction)
    {
        using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = DuckDb.ExistsAsTableSql;
        // Stryker disable once Statement : DuckDB.NET runs every command inside the connection's open transaction whether or not DbCommand.Transaction is set, so leaving it unset cannot be observed; it is set for ADO.NET conformance
        ((IDbCommand)cmd).Transaction = transaction;
        cmd.Parameters.Add(new DuckDBParameter(DuckDb.ExistsAsTableNameParameter, tableName));
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    private static void PromoteDirect(DuckDBConnection connection, string sql, DbTransaction? transaction)
    {
        CommandObservation.Log(sql, null);

        using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        // Stryker disable once Statement : DuckDB.NET runs every command inside the connection's open transaction whether or not DbCommand.Transaction is set, so leaving it unset cannot be observed; it is set for ADO.NET conformance
        ((IDbCommand)cmd).Transaction = transaction;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Promotes a VIEW source to a TABLE on first mutation asynchronously. No-op if already a table
    /// on this connection, or preloaded.
    /// </summary>
    /// <param name="connection">The DuckDB connection.</param>
    /// <param name="source">The file source to promote.</param>
    /// <param name="dialect">The DuckDB dialect for SQL generation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <param name="transaction">The caller's transaction from <c>CommandOptions</c>, if any.</param>
    /// <remarks>Same transaction and catalog handling as <see cref="EnsurePromotedToTable"/>.</remarks>
    public static async ValueTask EnsurePromotedToTableAsync(DuckDBConnection connection, IFileSource source, DuckDbDialect dialect, CancellationToken cancellationToken, IDbTransaction? transaction = null)
    {
        if (PreloadRegistry.IsPreloaded(connection, source))
            return;

        ConnectionState state = States.GetValue(connection, static _ => new ConnectionState());

        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (state.Promoted.Contains(source))
                return;

            DbTransaction? callerTransaction = AsyncTransactionValidator.RequireDbTransaction(transaction);
            DuckDBTransaction? ownTransaction = callerTransaction is null ? TryBeginOwnTransaction(connection) : null;
            DbTransaction? active = callerTransaction ?? ownTransaction;
            try
            {
                if (!await ExistsAsTableAsync(connection, source.TableName, active, cancellationToken).ConfigureAwait(false))
                {
                    var sql = dialect.GeneratePromoteToTableSql(source);

                    await CommandObservation.ExecuteAsync(sql, null, connection, DuckDbObservation.Text,
                        () => PromoteDirectAsync(connection, sql, active, cancellationToken),
                        cancellationToken).ConfigureAwait(false);
                }

                if (ownTransaction is null)
                    return;

                // Stryker disable once Boolean : DuckDB.NET completes this call synchronously (TheDuckDbDriverCompletesItsAsyncCallsSynchronously pins that), so no continuation is scheduled and ConfigureAwait has nothing to change
                await ownTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // No catch-and-rollback here, unlike EnsurePromotedToTable. There a caller's exception
                // filter runs before this method's finally, so the aborted transaction had to be rolled
                // back first. An async caller sees the exception only once the task faults, after this
                // Dispose has rolled the uncommitted transaction back.
                ownTransaction?.Dispose();
            }

            state.Promoted.Add(source);
            source.IsPromotedToTable = true;
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private static async ValueTask<bool> ExistsAsTableAsync(DuckDBConnection connection, string tableName, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        DuckDBCommand cmd = connection.CreateCommand();
        // Stryker disable once Boolean : DuckDB.NET completes this call synchronously (TheDuckDbDriverCompletesItsAsyncCallsSynchronously pins that), so no continuation is scheduled and ConfigureAwait has nothing to change
        await using var cmdDisposer = cmd.ConfigureAwait(false);
        cmd.CommandText = DuckDb.ExistsAsTableSql;
        // Stryker disable once Statement : DuckDB.NET runs every command inside the connection's open transaction whether or not DbCommand.Transaction is set, so leaving it unset cannot be observed; it is set for ADO.NET conformance
        ((IDbCommand)cmd).Transaction = transaction;
        cmd.Parameters.Add(new DuckDBParameter(DuckDb.ExistsAsTableNameParameter, tableName));
        // Stryker disable once Boolean : DuckDB.NET completes this call synchronously (TheDuckDbDriverCompletesItsAsyncCallsSynchronously pins that), so no continuation is scheduled and ConfigureAwait has nothing to change
        object? count = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(count, CultureInfo.InvariantCulture) > 0;
    }

    private static async ValueTask<object?> PromoteDirectAsync(DuckDBConnection connection, string sql, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        CommandObservation.Log(sql, null);

        DuckDBCommand cmd = connection.CreateCommand();
        // Stryker disable once Boolean : DuckDB.NET completes this call synchronously (TheDuckDbDriverCompletesItsAsyncCallsSynchronously pins that), so no continuation is scheduled and ConfigureAwait has nothing to change
        await using var cmdDisposer = cmd.ConfigureAwait(false);
        cmd.CommandText = sql;
        // Stryker disable once Statement : DuckDB.NET runs every command inside the connection's open transaction whether or not DbCommand.Transaction is set, so leaving it unset cannot be observed; it is set for ADO.NET conformance
        ((IDbCommand)cmd).Transaction = transaction;
        // Stryker disable once Boolean : DuckDB.NET completes this call synchronously (TheDuckDbDriverCompletesItsAsyncCallsSynchronously pins that), so no continuation is scheduled and ConfigureAwait has nothing to change
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return null;
    }
}
