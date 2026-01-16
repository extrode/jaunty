using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a query asynchronously and returns the single entity from the result set.
    /// Throws an exception if the result set is empty or contains more than one element.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The single entity of type T from the result set.</returns>
    public static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QuerySingleCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a query with parameters asynchronously and returns the single entity from the result set.
    /// Throws an exception if the result set is empty or contains more than one element.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The single entity of type T from the result set.</returns>
    public static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QuerySingleCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a query with command options asynchronously and returns the single entity from the result set.
    /// Throws an exception if the result set is empty or contains more than one element.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The single entity of type T from the result set.</returns>
    public static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QuerySingleCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a query with parameters and command options asynchronously and returns the single entity from the result set.
    /// Throws an exception if the result set is empty or contains more than one element.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The single entity of type T from the result set.</returns>
    public static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("The provided IDbConnection is not a DbConnection. Async operations require a DbConnection.")
            : QuerySingleCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
}
