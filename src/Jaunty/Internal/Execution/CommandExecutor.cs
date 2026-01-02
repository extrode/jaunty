using System.Data;
using System.Data.Common;

using Jaunty.Helpers;
using Jaunty.Internal.Parameters;

namespace Jaunty.Internal.Execution;

internal static class CommandExecutor
{
    public static TResult ExecuteReader<TResult>(
        IDbConnection connection,
        string sql,
        object? parameters,
        IDbTransaction? transaction,
        int? commandTimeout,
        Func<IDataReader, TResult> handler)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql.IsNullOrWhiteSpace()) throw new ArgumentException("SQL cannot be null or whitespace.", nameof(sql));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed) connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            if (transaction is not null)
                command.Transaction = transaction;

            if (commandTimeout.HasValue)
                command.CommandTimeout = commandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            using var reader = command.ExecuteReader();
            return handler(reader);
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    public static async Task<TResult> ExecuteReaderAsync<TResult>(
        IDbConnection connection,
        string sql,
        object? parameters,
        IDbTransaction? transaction,
        int? commandTimeout,
        Func<IDataReader, Task<TResult>> handler,
        CancellationToken cancellationToken)
    {
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql.IsNullOrWhiteSpace()) throw new ArgumentException("SQL cannot be null or whitespace.", nameof(sql));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        var wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed) await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using var command = dbConnection.CreateCommand();
                command.CommandText = sql;

                if (transaction is not null)
                    command.Transaction = transaction as DbTransaction;

                if (commandTimeout.HasValue)
                    command.CommandTimeout = commandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                return await handler(reader).ConfigureAwait(false);
            }
            else
            {
                // Fallback for non-DbConnection - use sync methods
                if (wasClosed) connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = sql;

                if (transaction is not null)
                    command.Transaction = transaction;

                if (commandTimeout.HasValue)
                    command.CommandTimeout = commandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                using var reader = command.ExecuteReader();
                return await handler(reader).ConfigureAwait(false);
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
}
