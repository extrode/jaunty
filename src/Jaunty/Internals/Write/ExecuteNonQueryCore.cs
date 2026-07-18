using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;
using Jaunty.Internals.Parameters;
using Jaunty.Interceptors;

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
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif

        // Use InterceptorPipeline if registered, otherwise execute directly
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            int result = 0;
            JauntyConfig.InterceptorPipeline.ExecuteWithInterception(
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

                        if (options.Transaction is not null)
                            command.Transaction = options.Transaction;

                        if (options.CommandTimeout.HasValue)
                            command.CommandTimeout = options.CommandTimeout.Value;

                        if (parameters is not null)
                            ParameterBinder.Bind(command, parameters);

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

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

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

    internal static async ValueTask<int> ExecuteNonQueryCoreAsync(IDbConnection connection, string sql, object? parameters, CommandOptions options, CommandType commandType, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif

        var dbConnection = connection as DbConnection;

        // Use InterceptorPipeline if registered, otherwise execute directly
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            int result = 0;
            await JauntyConfig.InterceptorPipeline.ExecuteWithInterceptionAsync(
                sql,
                parameters,
                connection,
                commandType,
                async () =>
                {
                    bool wasClosed = connection.State == ConnectionState.Closed;

                    try
                    {
                        if (dbConnection is not null)
                        {
                            if (wasClosed)
                                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                            DbCommand command = dbConnection.CreateCommand();
                            await using var commandDisposer = command.ConfigureAwait(false);
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

                            result = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // Fallback for non-DbConnection - use sync methods
                            if (wasClosed)
                                await Task.Run(() => connection.Open(), cancellationToken).ConfigureAwait(false);

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

                            result = command.ExecuteNonQuery();
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
        bool wasClosed = connection.State == ConnectionState.Closed;

        try
        {
            if (dbConnection is not null)
            {
                if (wasClosed)
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

#if NET8_0_OR_GREATER
                DbCommand command = dbConnection.CreateCommand();
                await using var commandDisposer = command.ConfigureAwait(false);
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

                JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

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

                JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

                return command.ExecuteNonQuery();
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
