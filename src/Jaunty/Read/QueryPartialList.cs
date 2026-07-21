using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and returns all results as dictionaries with column names as keys.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>A list of dictionaries, each representing a row with column names as keys.</returns>
    public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql)
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
        return QueryCoreList(connection, sql, null, default);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns all results as dictionaries with column names as keys.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>A list of dictionaries, each representing a row with column names as keys.</returns>
    public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, object parameters)
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
        return QueryCoreList(connection, sql, parameters, default);
    }

    /// <summary>
    /// Executes a SQL query and returns all results as dictionaries with column names as keys, using the
    /// specified command options (transaction, timeout, command type).
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Options controlling transaction, timeout, and command type.</param>
    /// <returns>A list of dictionaries, each representing a row with column names as keys.</returns>
    public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, CommandOptions options)
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
        return QueryCoreList(connection, sql, null, options);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns all results as dictionaries with column names as keys,
    /// using the specified command options (transaction, timeout, command type).
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">Options controlling transaction, timeout, and command type.</param>
    /// <returns>A list of dictionaries, each representing a row with column names as keys.</returns>
    public static List<IDictionary<string, object?>> QueryPartialList(this IDbConnection connection, string sql, object parameters, CommandOptions options)
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
        return QueryCoreList(connection, sql, parameters, options);
    }

    private static List<IDictionary<string, object?>> QueryCoreList(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
        // Use InterceptorPipeline if registered, otherwise execute directly - mirrors the
        // established pattern in GetAllCore.cs.
        if (JauntyConfig.InterceptorPipeline?.HasInterceptors == true)
        {
            return JauntyConfig.InterceptorPipeline.ExecuteWithInterception(
                sql,
                parameters,
                connection,
                options.CommandType,
                () => QueryCoreListDirect(connection, sql, parameters, options));
        }

        return QueryCoreListDirect(connection, sql, parameters, options);
    }

    private static List<IDictionary<string, object?>> QueryCoreListDirect(IDbConnection connection, string sql, object? parameters, CommandOptions options)
    {
        bool wasClosed = connection.State == ConnectionState.Closed;
        if (wasClosed)
            connection.Open();

        try
        {
            using IDbCommand command = connection.CreateCommand();
            command.CommandText = sql;

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

            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            command.BindParameters(parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            var results = new List<IDictionary<string, object?>>();

            using IDataReader reader = command.ExecuteReader();

            var columnNames = new string[reader.FieldCount];
            for (int i = 0; i < columnNames.Length; i++)
                columnNames[i] = reader.GetName(i);

            while (reader.Read())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < columnNames.Length; i++)
                {
                    object? value = reader.GetValue(i);
                    row[columnNames[i]] = value == DBNull.Value ? null : value;
                }
                results.Add(row);
            }

            return results;
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }
    }
}
