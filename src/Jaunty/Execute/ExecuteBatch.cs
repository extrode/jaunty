using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL command repeatedly for each set of parameters and returns the cumulative rows affected.
    /// </summary>
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
        bool wasClosed = connection.State == ConnectionState.Closed;
        int totalRowsAffected = 0;

        try
        {
            if (wasClosed)
                connection.Open();

            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

            if (commandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = commandType;

            if (options.Transaction is not null)
                command.Transaction = options.Transaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            bool prepared = false;
            foreach (var parameters in parameterSets)
            {
                if (parameters is null)
                    continue;

                command.Parameters.Clear();
                ParameterBinder.Bind(command, parameters);

                // Best-effort optimization, mirroring BulkInsertLoop: Prepare() once the first
                // parameter set has established the command's parameter shape, so providers that
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

    /// <summary>
    /// Executes a SQL command repeatedly for each set of parameters asynchronously.
    /// </summary>
    public static ValueTask<int> ExecuteBatchAsync(this IDbConnection connection, string sql, IEnumerable<object> parameterSets, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteBatchCoreAsync(dbConnection, sql, parameterSets, default, CommandType.Text, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL command repeatedly for each set of parameters with command options asynchronously.
    /// </summary>
    public static ValueTask<int> ExecuteBatchAsync(this IDbConnection connection, string sql, IEnumerable<object> parameterSets, CommandOptions options, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteBatchCoreAsync(dbConnection, sql, parameterSets, options, options.CommandType, cancellationToken);
    }

    internal static async ValueTask<int> ExecuteBatchCoreAsync(DbConnection connection, string sql, IEnumerable<object> parameterSets, CommandOptions options, CommandType commandType, CancellationToken cancellationToken)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        int totalRowsAffected = 0;

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

            if (commandType != CommandType.Text)
                command.CommandType = commandType;

            command.Transaction = AsyncTransactionValidator.RequireDbTransaction(options.Transaction);

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            bool prepared = false;
            foreach (var parameters in parameterSets)
            {
                if (parameters is null)
                    continue;

                command.Parameters.Clear();
                ParameterBinder.Bind(command, parameters);

                // Best-effort optimization, mirroring BulkInsertLoop: Prepare() once the first
                // parameter set has established the command's parameter shape, so providers that
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
