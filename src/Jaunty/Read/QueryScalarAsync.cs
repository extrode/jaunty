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
    /// This method is useful for aggregate queries that return a single value, such as 
    /// <c>COUNT</c>, <c>SUM</c>, <c>AVG</c>, <c>MIN</c>, or <c>MAX</c>.
    /// </para>
    /// <para>
    /// If the result set is empty, returns <see langword="default"/> for the type.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get total count
    /// var count = await connection.QueryScalarAsync&lt;long&gt;("SELECT COUNT(*) FROM products");
    /// 
    /// // Get sum with cancellation
    /// using var cts = new CancellationTokenSource();
    /// var total = await connection.QueryScalarAsync&lt;decimal&gt;(
    ///     "SELECT SUM(price) FROM products",
    ///     cts.Token);
    /// </code>
    /// </example>
    /// <seealso cref="QueryScalarAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    /// <seealso cref="QueryScalar{T}(IDbConnection, string)"/>
    /// <seealso cref="ExecuteScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
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
    /// This method is useful for aggregate queries with parameters that return a single value.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get count with filter
    /// var count = await connection.QueryScalarAsync&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryScalar{T}(IDbConnection, string, object)"/>
    public static ValueTask<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
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
    /// Use this overload when you need to execute the query within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get count within transaction
    /// using var tx = connection.BeginTransaction();
    /// var count = await connection.QueryScalarAsync&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products",
    ///     CommandOptions&lt;long&gt;.WithTransaction(tx));
    ///
    /// // Get count with timeout
    /// var count = await connection.QueryScalarAsync&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products",
    ///     CommandOptions&lt;long&gt;.WithTimeout(30));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default)
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
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var count = await connection.QueryScalarAsync&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;long&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<T> QueryScalarAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(parameters);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryScalarCoreAsync(dbConnection, sql, parameters, options, cancellationToken);
    }
}