using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the value of the first column of the first row, converted to type <typeparamref name="T"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is an alias for <see cref="QueryScalarAsync{T}(IDbConnection, string, CancellationToken)"/> and behaves identically.
    /// It is useful for aggregate queries that return a single value, such as 
    /// <c>COUNT</c>, <c>SUM</c>, <c>AVG</c>, <c>MIN</c>, or <c>MAX</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get total count
    /// var count = await connection.ExecuteScalarAsync&lt;long&gt;("SELECT COUNT(*) FROM products");
    /// 
    /// // Get sum with cancellation
    /// using var cts = new CancellationTokenSource();
    /// var total = await connection.ExecuteScalarAsync&lt;decimal&gt;(
    ///     "SELECT SUM(price) FROM products",
    ///     cts.Token);
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="ExecuteScalar{T}(IDbConnection, string)"/>
    public static ValueTask<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryScalarCoreAsync<T>(dbConnection, sql, null, default, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the value of the first column of the first row, converted to type <typeparamref name="T"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is an alias for <see cref="QueryScalarAsync{T}(IDbConnection, string, object, CancellationToken)"/> and behaves identically.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get count with filter
    /// var count = await connection.ExecuteScalarAsync&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="ExecuteScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryScalarAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryScalarCoreAsync<T>(dbConnection, sql, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with command options asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the value of the first column of the first row, converted to type <typeparamref name="T"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is an alias for <see cref="QueryScalarAsync{T}(IDbConnection, string, CommandOptions{T}, CancellationToken)"/> and behaves identically.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get count within transaction
    /// using var tx = connection.BeginTransaction();
    /// var count = await connection.ExecuteScalarAsync&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products",
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryScalarCoreAsync(dbConnection, sql, null, options, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options asynchronously and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the value of the first column of the first row, converted to type <typeparamref name="T"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is an alias for <see cref="QueryScalarAsync{T}(IDbConnection, string, object, CommandOptions{T}, CancellationToken)"/> and behaves identically.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var count = await connection.ExecuteScalarAsync&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="ExecuteScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryScalarCoreAsync(dbConnection, sql, parameters, options, cancellationToken);
    }
}