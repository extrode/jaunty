using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a query asynchronously and returns the first entity from the result set or null if the result set is empty.
    /// Uses partial mapping mode where only properties with matching columns in the result set are mapped.
    /// Properties without matching columns are left with their default values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first entity of type T from the result set, or null if the result set is empty.</returns>
    public static Task<T?> QueryPartialFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Executes a query with parameters asynchronously and returns the first entity from the result set or null if the result set is empty.
    /// Uses partial mapping mode where only properties with matching columns in the result set are mapped.
    /// Properties without matching columns are left with their default values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first entity of type T from the result set, or null if the result set is empty.</returns>
    public static Task<T?> QueryPartialFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Executes a query with command options asynchronously and returns the first entity from the result set or null if the result set is empty.
    /// Uses partial mapping mode where only properties with matching columns in the result set are mapped.
    /// Properties without matching columns are left with their default values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first entity of type T from the result set, or null if the result set is empty.</returns>
    public static Task<T?> QueryPartialFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Executes a query with parameters and command options asynchronously and returns the first entity from the result set or null if the result set is empty.
    /// Uses partial mapping mode where only properties with matching columns in the result set are mapped.
    /// Properties without matching columns are left with their default values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="connection">The database connection.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">Parameters for the query.</param>
    /// <param name="options">Command options (transaction, timeout, custom mapper).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The first entity of type T from the result set, or null if the result set is empty.</returns>
    public static Task<T?> QueryPartialFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Projection, cancellationToken);
    }
}
