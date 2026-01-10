using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    private static async Task<TResult> ExecuteReaderAsync<TResult>(IDbConnection connection, string sql, object? parameters,
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
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif
        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using var command = dbConnection.CreateCommand();
                command.CommandText = sql;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                return await handler(reader, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Fallback for non-DbConnection - use sync methods
                if (wasClosed) connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = sql;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

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

