using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    internal static int ExecuteNonQueryCore(IDbConnection connection, string sql, object? parameters, CommandOptions options, CommandType commandType)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            // Only set CommandType for stored procedures - SQLite doesn't support setting CommandType
            if (commandType == CommandType.StoredProcedure || commandType == CommandType.TableDirect)
                command.CommandType = commandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            return command.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    internal static async ValueTask<int> ExecuteNonQueryCoreAsync(IDbConnection connection, string sql, object? parameters, CommandOptions options, CommandType commandType, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif

        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (connection is DbConnection dbConnection)
            {
                if (wasClosed)
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                await using DbCommand command = dbConnection.CreateCommand();
#else
                using DbCommand command = dbConnection.CreateCommand();
#endif
                command.CommandText = sql;

                // Only set CommandType if not default (Text) - SQLite doesn't support setting CommandType
                if (commandType != CommandType.Text)
                    command.CommandType = commandType;

                if (options.Transaction is DbTransaction dbTransaction)
                    command.Transaction = dbTransaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Fallback for non-DbConnection - use sync methods
                if (wasClosed)
                    connection.Open();

                using IDbCommand command = connection.CreateCommand();
                command.CommandText = sql;

                // Only set CommandType if not default (Text) - SQLite doesn't support setting CommandType
                if (commandType != CommandType.Text)
                    command.CommandType = commandType;

                if (options.Transaction is not null)
                    command.Transaction = options.Transaction;

                if (options.CommandTimeout.HasValue)
                    command.CommandTimeout = options.CommandTimeout.Value;

                if (parameters is not null)
                    ParameterBinder.Bind(command, parameters);

                Configuration.JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

                return command.ExecuteNonQuery();
            }
        }
        finally
        {
            if (wasClosed && connection.State != ConnectionState.Closed)
            {
#if NET8_0_OR_GREATER
                if (connection is DbConnection dbConn)
                    await dbConn.CloseAsync().ConfigureAwait(false);
                else
                    connection.Close();
#else
                connection.Close();
#endif
            }
        }
    }
}


