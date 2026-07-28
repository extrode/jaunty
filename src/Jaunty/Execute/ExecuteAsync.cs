using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL command (INSERT, UPDATE, DELETE, DDL, etc.) asynchronously and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the command against. Must be a DbConnection for async execution.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation. Defaults to CancellationToken.None.</param>
    /// <returns>A ValueTask containing the number of rows affected by the command.</returns>
    /// <remarks>
    /// <para>
    /// This method is the async equivalent of Execute. It returns a ValueTask for efficient async patterns.
    /// </para>
    /// </remarks>
    public static ValueTask<int> ExecuteAsync(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteNonQueryCoreAsync(dbConnection, sql, null, default, CommandType.Text, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL command with parameters asynchronously and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the command against. Must be a DbConnection for async execution.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">An anonymous object or dictionary containing parameter values.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation. Defaults to CancellationToken.None.</param>
    /// <returns>A ValueTask containing the number of rows affected by the command.</returns>
    public static ValueTask<int> ExecuteAsync(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteNonQueryCoreAsync(dbConnection, sql, parameters, default, CommandType.Text, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL command with command options asynchronously and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the command against. Must be a DbConnection for async execution.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="options">Command execution options, including transaction, timeout, and command type.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation. Defaults to CancellationToken.None.</param>
    /// <returns>A ValueTask containing the number of rows affected by the command.</returns>
    public static ValueTask<int> ExecuteAsync(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default)
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteNonQueryCoreAsync(dbConnection, sql, null, options, options.CommandType, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL command with parameters and command options asynchronously, returning the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the command against. Must be a DbConnection for async execution.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">An anonymous object or dictionary containing parameter values.</param>
    /// <param name="options">Command execution options, including transaction, timeout, and command type.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation. Defaults to CancellationToken.None.</param>
    /// <returns>A ValueTask containing the number of rows affected by the command.</returns>
    public static ValueTask<int> ExecuteAsync(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteNonQueryCoreAsync(dbConnection, sql, parameters, options, options.CommandType, cancellationToken);
    }
}
