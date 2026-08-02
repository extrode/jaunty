using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Parameters;
using Jaunty.Internals;
using Jaunty.Internals.Write;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL command repeatedly for each set of parameters and returns the cumulative rows affected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every set is executed. A <see langword="null"/> entry in <paramref name="parameterSets"/>
    /// throws <see cref="ArgumentException"/> naming its index - it was previously skipped in
    /// silence, which left the caller unable to tell "one of your sets was null and I ignored it"
    /// from "that statement affected no rows" (AUD-R26). Pass an empty object for a statement that
    /// genuinely takes no parameters.
    /// </para>
    /// </remarks>
    public static int ExecuteBatch(this IDbConnection connection, string sql, IEnumerable<object> parameterSets)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameterSets);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameterSets is null) throw new ArgumentNullException(nameof(parameterSets));
#endif
        return ExecuteBatchCore(connection, sql, parameterSets, default, CommandType.Text);
    }

    /// <summary>
    /// Executes a SQL command repeatedly for each set of parameters with command options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every set is executed. A <see langword="null"/> entry in <paramref name="parameterSets"/>
    /// throws <see cref="ArgumentException"/> naming its index - it was previously skipped in
    /// silence, which left the caller unable to tell "one of your sets was null and I ignored it"
    /// from "that statement affected no rows" (AUD-R26). Pass an empty object for a statement that
    /// genuinely takes no parameters.
    /// </para>
    /// </remarks>
    public static int ExecuteBatch(this IDbConnection connection, string sql, IEnumerable<object> parameterSets, CommandOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameterSets);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameterSets is null) throw new ArgumentNullException(nameof(parameterSets));
#endif
        return ExecuteBatchCore(connection, sql, parameterSets, options, options.CommandType);
    }

    internal static int ExecuteBatchCore(IDbConnection connection, string sql, IEnumerable<object> parameterSets, CommandOptions options, CommandType commandType)
    {
        // AUD-R26: ExecuteBatch executed inline in this file and so reached neither the interceptor
        // pipeline nor the logger. Reported once for the whole batch rather than once per set: the
        // batch is one logical operation, and the count is only stated when the caller's sequence
        // already knows it - draining a lazy IEnumerable to fill in a log line would change this
        // method's memory behaviour.
        var batchParameters = new BulkOperationParameters(
            "ExecuteBatch", null, parameterSets is ICollection<object> known ? known.Count : (int?)null);

        return CommandObservation.Execute(sql, batchParameters, connection, commandType, Body);

        int Body()
        {
        bool wasClosed = connection.State == ConnectionState.Closed;
        int totalRowsAffected = 0;

        CommandObservation.Log(sql, batchParameters);

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (commandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = commandType;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring GetByIdSimpleCoreDirect) so an incompatible
            // transaction gets Jaunty's clear ArgumentException instead.
            if (options.Transaction is not null)
            {
                command.Transaction = connection is DbConnection
                    ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                    : options.Transaction;
            }

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            bool prepared = false;
            int setIndex = -1;
            Type? boundType = null;

            foreach (var parameters in parameterSets)
            {
                setIndex++;

                // AUD-R26: this was `if (parameters is null) continue;`. The caller got a lower
                // cumulative row count with nothing to distinguish "one of your parameter sets was
                // null and I ignored it" from "that statement affected no rows" - a strictly larger
                // silent drop than the two the parameter layer already refuses to make. Compare
                // ParameterBinder.Bind ("silently binding zero parameters would execute the
                // procedure with the value dropped. Fail loudly instead") and BindScalar ("fail fast
                // when the caller passed a value but the SQL contains no matching placeholder,
                // instead of silently dropping it"). Dropping an entire set was worse than either.
                if (parameters is null)
                {
                    throw new ArgumentException(
                        $"Parameter set at index {setIndex} is null. ExecuteBatch executes one statement per set and cannot execute a null one; remove it from the sequence, or pass an empty object if the statement genuinely takes no parameters.",
                        nameof(parameterSets));
                }

                // AUD-R26: the Prepare() comment below used to claim this loop mirrored
                // BulkInsertLoop, while the loop cleared and fully rebound on every set - so a batch
                // of N sets with M parameters allocated N x M provider parameter objects, on the API
                // whose whole purpose is high-volume repetition, and Prepare() was called on a
                // command whose parameter collection was then torn down on the next iteration.
                // BulkInsertLoop does the opposite and that is the point of it: bind once, then set
                // .Value in place. TryRebind is that, for the shapes where it is safe.
                //
                // The runtime-type check is this loop's responsibility, not TryRebind's: the cached
                // templates deliberately carry no DbType (the provider infers it from the value), so
                // reusing parameter objects across sets of different types could leave a parameter
                // holding a type the provider inferred from a previous set's value.
                Type parameterType = parameters.GetType();

                // AUD-R34-009: ParameterBinder.Bind rewrites command.CommandText when a set contains
                // a collection to expand into an IN clause, and CommandText was assigned once above
                // the loop - so the next set was bound against the previous set's expanded text,
                // `IN (@Ids0, @Ids1)`, which names parameters no property matches. Expansion is
                // per-set by construction, so an expanded text can never be reused: restore the
                // original SQL and rebind from scratch.
                bool expanded = !string.Equals(command.CommandText, sql, StringComparison.Ordinal);

                if (expanded || parameterType != boundType || !ParameterBinder.TryRebind(command, parameters))
                {
                    if (expanded)
                        command.CommandText = sql;

                    command.Parameters.Clear();
                    ParameterBinder.Bind(command, parameters);
                    boundType = parameterType;

                    // The plan prepared for the previous set describes text and a parameter shape
                    // that no longer exist. `prepared` used to latch true for the whole batch.
                    prepared = false;
                }

                // Best-effort optimization, now genuinely mirroring BulkInsertLoop: Prepare() once
                // the parameter set has established the command's parameter shape, so providers that
                // cache a compiled plan for repeated CommandText don't recompile it every row.
                if (!prepared)
                {
                    try { command.Prepare(); } catch { /* Best effort — not all providers support this */ }
                    prepared = true;
                }

                totalRowsAffected += command.ExecuteNonQuery();
            }

            return totalRowsAffected;
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
        }
    }

    /// <summary>
    /// Executes a SQL command repeatedly for each set of parameters asynchronously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every set is executed. A <see langword="null"/> entry in <paramref name="parameterSets"/>
    /// throws <see cref="ArgumentException"/> naming its index - it was previously skipped in
    /// silence, which left the caller unable to tell "one of your sets was null and I ignored it"
    /// from "that statement affected no rows" (AUD-R26). Pass an empty object for a statement that
    /// genuinely takes no parameters.
    /// </para>
    /// </remarks>
    public static ValueTask<int> ExecuteBatchAsync(this IDbConnection connection, string sql, IEnumerable<object> parameterSets, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameterSets);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameterSets is null) throw new ArgumentNullException(nameof(parameterSets));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteBatchCoreAsync(dbConnection, sql, parameterSets, default, CommandType.Text, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL command repeatedly for each set of parameters with command options asynchronously.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every set is executed. A <see langword="null"/> entry in <paramref name="parameterSets"/>
    /// throws <see cref="ArgumentException"/> naming its index - it was previously skipped in
    /// silence, which left the caller unable to tell "one of your sets was null and I ignored it"
    /// from "that statement affected no rows" (AUD-R26). Pass an empty object for a statement that
    /// genuinely takes no parameters.
    /// </para>
    /// </remarks>
    public static ValueTask<int> ExecuteBatchAsync(this IDbConnection connection, string sql, IEnumerable<object> parameterSets, CommandOptions options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameterSets);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameterSets is null) throw new ArgumentNullException(nameof(parameterSets));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteBatchCoreAsync(dbConnection, sql, parameterSets, options, options.CommandType, cancellationToken);
    }

    internal static ValueTask<int> ExecuteBatchCoreAsync(DbConnection connection, string sql, IEnumerable<object> parameterSets, CommandOptions options, CommandType commandType, CancellationToken cancellationToken)
    {
        // AUD-R26 - see the comment on the synchronous ExecuteBatchCore.
        var batchParameters = new BulkOperationParameters(
            "ExecuteBatch", null, parameterSets is ICollection<object> known ? known.Count : (int?)null);

        return CommandObservation.ExecuteAsync(sql, batchParameters, connection, commandType, Body, cancellationToken);

        async ValueTask<int> Body()
        {
        bool wasClosed = connection.State == ConnectionState.Closed;
        int totalRowsAffected = 0;

        CommandObservation.Log(sql, batchParameters);

        try
        {
            if (wasClosed)
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
            DbCommand command = connection.CreateCommand();
            await using var commandDisposer = command.ConfigureAwait(false);
#else
            using DbCommand command = connection.CreateCommand();
#endif
            command.CommandText = sql;

            if (commandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = commandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            bool prepared = false;
            int setIndex = -1;
            Type? boundType = null;

            foreach (var parameters in parameterSets)
            {
                setIndex++;

                // AUD-R26: this was `if (parameters is null) continue;`. The caller got a lower
                // cumulative row count with nothing to distinguish "one of your parameter sets was
                // null and I ignored it" from "that statement affected no rows" - a strictly larger
                // silent drop than the two the parameter layer already refuses to make. Compare
                // ParameterBinder.Bind ("silently binding zero parameters would execute the
                // procedure with the value dropped. Fail loudly instead") and BindScalar ("fail fast
                // when the caller passed a value but the SQL contains no matching placeholder,
                // instead of silently dropping it"). Dropping an entire set was worse than either.
                if (parameters is null)
                {
                    throw new ArgumentException(
                        $"Parameter set at index {setIndex} is null. ExecuteBatch executes one statement per set and cannot execute a null one; remove it from the sequence, or pass an empty object if the statement genuinely takes no parameters.",
                        nameof(parameterSets));
                }

                // AUD-R26: the Prepare() comment below used to claim this loop mirrored
                // BulkInsertLoop, while the loop cleared and fully rebound on every set - so a batch
                // of N sets with M parameters allocated N x M provider parameter objects, on the API
                // whose whole purpose is high-volume repetition, and Prepare() was called on a
                // command whose parameter collection was then torn down on the next iteration.
                // BulkInsertLoop does the opposite and that is the point of it: bind once, then set
                // .Value in place. TryRebind is that, for the shapes where it is safe.
                //
                // The runtime-type check is this loop's responsibility, not TryRebind's: the cached
                // templates deliberately carry no DbType (the provider infers it from the value), so
                // reusing parameter objects across sets of different types could leave a parameter
                // holding a type the provider inferred from a previous set's value.
                Type parameterType = parameters.GetType();

                // AUD-R34-009: ParameterBinder.Bind rewrites command.CommandText when a set contains
                // a collection to expand into an IN clause, and CommandText was assigned once above
                // the loop - so the next set was bound against the previous set's expanded text,
                // `IN (@Ids0, @Ids1)`, which names parameters no property matches. Expansion is
                // per-set by construction, so an expanded text can never be reused: restore the
                // original SQL and rebind from scratch.
                bool expanded = !string.Equals(command.CommandText, sql, StringComparison.Ordinal);

                if (expanded || parameterType != boundType || !ParameterBinder.TryRebind(command, parameters))
                {
                    if (expanded)
                        command.CommandText = sql;

                    command.Parameters.Clear();
                    ParameterBinder.Bind(command, parameters);
                    boundType = parameterType;

                    // The plan prepared for the previous set describes text and a parameter shape
                    // that no longer exist. `prepared` used to latch true for the whole batch.
                    prepared = false;
                }

                // Best-effort optimization, now genuinely mirroring BulkInsertLoop: Prepare() once
                // the parameter set has established the command's parameter shape, so providers that
                // cache a compiled plan for repeated CommandText don't recompile it every row.
                if (!prepared)
                {
                    try { command.Prepare(); } catch { /* Best effort — not all providers support this */ }
                    prepared = true;
                }

                totalRowsAffected += await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            return totalRowsAffected;
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => connection.Close()).ConfigureAwait(false);
#endif
            }
        }
        }
    }
}
