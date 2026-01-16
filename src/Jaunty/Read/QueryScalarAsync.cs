using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a query asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value of the first column of the first row in the result set.</returns>
    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryScalarCoreAsync<T>(dbConnection, sql, null, default, cancellationToken);
    }

    /// <summary>
    /// Executes a query with parameters asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value of the first column of the first row in the result set.</returns>
    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryScalarCoreAsync<T>(dbConnection, sql, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Executes a query with command options asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value of the first column of the first row in the result set.</returns>
    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryScalarCoreAsync<T>(dbConnection, sql, null, options, cancellationToken);
    }

    /// <summary>
    /// Executes a query with parameters and command options asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The return type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value of the first column of the first row in the result set.</returns>
    public static Task<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QueryScalarCoreAsync<T>(dbConnection, sql, parameters, options, cancellationToken);
    }
}
