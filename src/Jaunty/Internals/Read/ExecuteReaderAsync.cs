using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static async ValueTask<TResult> ExecuteReaderAsync<TResult>(IDbConnection connection, string sql, object? parameters,
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
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                await using DbCommand command = dbConnection.CreateCommand();
#else
                using DbCommand command = dbConnection.CreateCommand();
#endif
                command.CommandText = sql;

                // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
                // Note: default(CommandType) is 0, CommandType.Text is 1, so check for both
                if (options.CommandType == CommandType.StoredProcedure || options.CommandType == CommandType.TableDirect)
                    command.CommandType = options.CommandType;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

#if NET8_0_OR_GREATER
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#else
                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
                return await handler(reader, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Fallback for non-DbConnection - use sync methods
                if (wasClosed) connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = sql;

                // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
                // Note: default(CommandType) is 0, CommandType.Text is 1, so check for both
                if (options.CommandType == CommandType.StoredProcedure || options.CommandType == CommandType.TableDirect)
                    command.CommandType = options.CommandType;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

                using var reader = command.ExecuteReader();
                return await handler(reader, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                if (connection is DbConnection dbConn)
                    await dbConn.CloseAsync().ConfigureAwait(false);
#else
                connection.Close();
#endif
            }
        }
    }
}



