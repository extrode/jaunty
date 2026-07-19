using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Parameters;
using Jaunty.Interceptors;

namespace Jaunty;

public static partial class Jaunty
{
    // Internal (not private) so tests can exercise the non-DbConnection fallback branch directly:
    // every current public async entry point rejects non-DbConnection connections before reaching
    // here, so this path is otherwise unreachable from outside the assembly.
    internal static async ValueTask<TResult> ExecuteReaderAsync<TResult>(IDbConnection connection, string sql, object? parameters,
            CommandOptions options, Func<IDataReader, CancellationToken, Task<TResult>> handler, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        var dbConnection = connection as DbConnection;

        // Use InterceptorPipeline if registered, otherwise execute directly
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            TResult result = default!;
            await JauntyConfig.InterceptorPipeline.ExecuteWithInterceptionAsync(
                sql,
                parameters,
                connection,
                options.CommandType,
                async () =>
                {
                    var wasClosed = connection.State == ConnectionState.Closed;

                    try
                    {
                        if (dbConnection is not null)
                        {
                            if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                            DbCommand command = dbConnection.CreateCommand();
                            await using var commandDisposer = command.ConfigureAwait(false);
#else
                            using DbCommand command = dbConnection.CreateCommand();
#endif
                            command.CommandText = sql;

                            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                                command.CommandType = options.CommandType;

                            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

                            if (options.CommandTimeout.HasValue)
                                command.CommandTimeout = options.CommandTimeout.Value;

                            if (parameters is not null)
                                ParameterBinder.Bind(command, parameters);

#if NET8_0_OR_GREATER
                            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                            await using var readerDisposer = reader.ConfigureAwait(false);
#else
                            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
                            result = await handler(reader, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // Fallback for non-DbConnection - use sync methods wrapped in Task.Run
                            if (wasClosed)
                                await Task.Run(() => connection.Open(), cancellationToken).ConfigureAwait(false);

                            using IDbCommand command = connection.CreateCommand();
                            command.CommandText = sql;

                            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                                command.CommandType = options.CommandType;

                            if (options.Transaction is not null)
                                command.Transaction = options.Transaction;

                            if (options.CommandTimeout.HasValue)
                                command.CommandTimeout = options.CommandTimeout.Value;

                            if (parameters is not null)
                                ParameterBinder.Bind(command, parameters);

                            using IDataReader reader = command.ExecuteReader();
                            result = await handler(reader, cancellationToken).ConfigureAwait(false);
                        }
                        return result;
                    }
                    finally
                    {
                        if (wasClosed && connection.State != ConnectionState.Closed)
                        {
#if NET8_0_OR_GREATER
                            if (dbConnection is not null)
                                await dbConnection.CloseAsync().ConfigureAwait(false);
                            else
                                await Task.Run(() => connection.Close()).ConfigureAwait(false);
#else
                            await Task.Run(() => connection.Close()).ConfigureAwait(false);
#endif
                        }
                    }
                },
                cancellationToken).ConfigureAwait(false);
            return result;
        }

        // Fast path: no interceptors, direct execution
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (dbConnection is not null)
            {
                if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                DbCommand command = dbConnection.CreateCommand();
                await using var commandDisposer = command.ConfigureAwait(false);
#else
                using DbCommand command = dbConnection.CreateCommand();
#endif
                command.CommandText = sql;

                // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
                // Note: default(CommandType) is 0, CommandType.Text is 1, so check for both
                if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                    command.CommandType = options.CommandType;

                command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using var readerDisposer = reader.ConfigureAwait(false);
#else
                using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
                return await handler(reader, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Fallback for non-DbConnection - use sync methods wrapped in Task.Run to avoid blocking
                if (wasClosed)
                    await Task.Run(() => connection.Open(), cancellationToken).ConfigureAwait(false);

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
                // Note: default(CommandType) is 0, CommandType.Text is 1, so check for both
                if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                    command.CommandType = options.CommandType;

                if (options.Transaction is not null)
                    command.Transaction = options.Transaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

                using IDataReader reader = command.ExecuteReader();
                return await handler(reader, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                if (dbConnection is not null)
                    await dbConnection.CloseAsync().ConfigureAwait(false);
                else
                    await Task.Run(() => connection.Close()).ConfigureAwait(false);
#else
                await Task.Run(() => connection.Close()).ConfigureAwait(false);
#endif
            }
        }
    }
}
