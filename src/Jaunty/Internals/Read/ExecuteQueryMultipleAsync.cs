using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Interceptors;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static async ValueTask<GridReader> ExecuteQueryMultipleAsync(DbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif

        // Use InterceptorPipeline if registered, otherwise execute directly. Unlike streaming,
        // ExecuteReaderAsync() here fully executes the command against the database in one round
        // trip (the multiple result sets are already available); GridReader only lazily walks rows
        // the caller already has. So wrapping just command-creation-through-ExecuteReaderAsync() is
        // safe and matches the interceptor semantics used by the other Query*Async cores, without
        // requiring the grid's result sets to be materialized upfront.
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            return await JauntyConfig.InterceptorPipeline.ExecuteWithInterceptionAsync(
                sql,
                parameters,
                connection,
                options.CommandType,
                () => ExecuteQueryMultipleDirectAsync(connection, sql, parameters, options, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await ExecuteQueryMultipleDirectAsync(connection, sql, parameters, options, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<GridReader> ExecuteQueryMultipleDirectAsync(DbConnection connection, string sql, object? parameters, CommandOptions options, CancellationToken cancellationToken)
    {
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Do NOT use 'using' — the GridReader owns the command and disposes it.
        DbCommand command = connection.CreateCommand();

        try
        {
            command.CommandText = sql;

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            // Do NOT use 'using' — the GridReader owns the reader and disposes it.
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return new GridReader(reader, connection, wasClosed, command);
        }
        catch
        {
            command.Dispose();
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                await connection.CloseAsync().ConfigureAwait(false);
#else
                await Task.Run(() => connection.Close()).ConfigureAwait(false);
#endif
            }
            throw;
        }
    }
}