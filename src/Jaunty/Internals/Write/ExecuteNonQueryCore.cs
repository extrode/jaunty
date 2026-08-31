using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Parameters;
using Jaunty.Interceptors;
using Jaunty.Internals;

namespace Jaunty;

public static partial class Jaunty
{
    internal static int ExecuteNonQueryCore(IDbConnection connection, string sql, object? parameters, CommandOptions options, CommandType commandType)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif

        // Use InterceptorPipeline if registered, otherwise execute directly
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            int result = 0;
            pipeline.ExecuteWithInterception(
                sql,
                parameters,
                connection,
                commandType,
                () =>
                {
                    bool wasClosed = connection.State == ConnectionState.Closed;

                    try
                    {
                        if (wasClosed) connection.Open();

                        using IDbCommand command = connection.CreateCommand();
                        command.CommandText = sql;

                        // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
                        if (commandType is CommandType.StoredProcedure or CommandType.TableDirect)
                            command.CommandType = commandType;

                        // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
                        // implementation, which casts to DbTransaction internally - assigning a
                        // non-DbTransaction IDbTransaction through it throws an opaque
                        // InvalidCastException. Validate via AsyncTransactionValidator first (mirroring
                        // GetByIdSimpleCoreDirect) so an incompatible transaction gets Jaunty's clear
                        // ArgumentException instead.
                        if (options.Transaction is not null)
                        {
                            command.Transaction = connection is DbConnection
                                ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                                : options.Transaction;
                        }

                        if (options.CommandTimeout.HasValue)
                            command.CommandTimeout = options.CommandTimeout.Value;

                        if (parameters is not null)
                            ParameterBinder.Bind(command, parameters);

                        JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

                        result = command.ExecuteNonQuery();
                        return result;
                    }
                    finally
                    {
                        if (wasClosed && connection.State != ConnectionState.Closed)
                            connection.Close();
                    }
                });
            return result;
        }

        // Fast path: no interceptors, direct execution
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
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

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    /// <remarks>
    /// AUD-R35-130. This took an <see cref="IDbConnection"/> and carried a
    /// "Fallback for non-DbConnection - use sync methods" arm in both the pipeline closure and the
    /// fast path, neither of which could run: all five call sites - the four in
    /// <c>ExecuteAsync.cs</c> and <c>ExecuteStoredProcedureNonQueryAsync</c> - already do
    /// <c>connection is not DbConnection dbConnection ? throw ... : ExecuteNonQueryCoreAsync(dbConnection, ...)</c>.
    /// The dead arms had also drifted: each assigned <c>command.Transaction = options.Transaction</c>
    /// with none of the <see cref="AsyncTransactionValidator.RequireDbTransaction"/> checking that
    /// AUD-R3-001 standardised, so the copy nobody could reach was the copy that had lost the guard -
    /// which is the argument against keeping unreachable arms, not for it. Filed identically in
    /// rounds 33 and 34 and never acted on. The parameter type now states the invariant, so the
    /// compiler keeps it rather than a comment. The <c>Task.Run</c> wrapping around the pre-net8
    /// <c>Close</c> went with them, for the AUD-R35-123 reason: a <c>Task.Run</c> around a blocking
    /// call still blocks a thread-pool thread.
    /// </remarks>
    internal static async ValueTask<int> ExecuteNonQueryCoreAsync(DbConnection connection, string sql, object? parameters, CommandOptions options, CommandType commandType, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif

        // Use InterceptorPipeline if registered, otherwise execute directly
        // One resolution, one read: CommandObservation decides whether anything is watching
        // (interceptor, diagnostics subscriber, or both) and hands back what to route through.
        InterceptorPipeline? pipeline = CommandObservation.Observer;

        if (pipeline is not null)
        {
            int result = 0;
            await pipeline.ExecuteWithInterceptionAsync(
                sql,
                parameters,
                connection,
                commandType,
                async () =>
                {
                    bool wasClosed = connection.State == ConnectionState.Closed;

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

                        // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
                        if (commandType is CommandType.StoredProcedure or CommandType.TableDirect)
                            command.CommandType = commandType;

                        command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

                        if (options.CommandTimeout.HasValue)
                            command.CommandTimeout = options.CommandTimeout.Value;

                        if (parameters is not null)
                            ParameterBinder.Bind(command, parameters);

                        JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

                        result = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        return result;
                    }
                    finally
                    {
                        if (wasClosed && connection.State != ConnectionState.Closed)
                        {
#if NET8_0_OR_GREATER
                            await connection.CloseAsync().ConfigureAwait(false);
#else
                            connection.Close();
#endif
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);
            return result;
        }

        // Fast path: no interceptors, direct execution
        bool wasClosed = connection.State == ConnectionState.Closed;

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

            // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
            if (commandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = commandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }
}
