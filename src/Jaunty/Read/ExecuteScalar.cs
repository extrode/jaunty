using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>The value of the first column of the first row, converted to type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method is an alias for <see cref="QueryScalar{T}(IDbConnection, string)"/> and behaves identically.
    /// It is useful for aggregate queries that return a single value, such as 
    /// <c>COUNT</c>, <c>SUM</c>, <c>AVG</c>, <c>MIN</c>, or <c>MAX</c>.
    /// </para>
    /// <para>
    /// If the result set is empty, returns <see langword="default"/> for the type.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get total count
    /// var count = connection.ExecuteScalar&lt;long&gt;("SELECT COUNT(*) FROM products");
    /// 
    /// // Get sum
    /// var total = connection.ExecuteScalar&lt;decimal&gt;("SELECT SUM(price) FROM products");
    /// 
    /// // Get maximum value
    /// var maxPrice = connection.ExecuteScalar&lt;decimal&gt;("SELECT MAX(price) FROM products");
    /// </code>
    /// </example>
    /// <seealso cref="QueryScalar{T}(IDbConnection, string)"/>
    /// <seealso cref="ExecuteScalar{T}(IDbConnection, string, object)"/>
    /// <seealso cref="ExecuteScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static T ExecuteScalar<T>(this IDbConnection connection, string sql)
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
        return QueryScalarCore<T>(connection, sql, null, default);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>The value of the first column of the first row, converted to type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method is an alias for <see cref="QueryScalar{T}(IDbConnection, string, object)"/> and behaves identically.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get count with filter
    /// var count = connection.ExecuteScalar&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="ExecuteScalar{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryScalar{T}(IDbConnection, string, object)"/>
    public static T ExecuteScalar<T>(this IDbConnection connection, string sql, object parameters)
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
        return QueryScalarCore<T>(connection, sql, parameters, default);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>The value of the first column of the first row, converted to type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method is an alias for <see cref="QueryScalar{T}(IDbConnection, string, CommandOptions{T})"/> and behaves identically.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get count within transaction
    /// using var tx = connection.BeginTransaction();
    /// var count = connection.ExecuteScalar&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products",
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteScalar{T}(IDbConnection, string)"/>
    public static T ExecuteScalar<T>(this IDbConnection connection, string sql, CommandOptions<T> options)
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
        return QueryScalarCore(connection, sql, null, options);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and returns the first column of the first row in the result set.
    /// </summary>
    /// <typeparam name="T">The type to convert the result to.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>The value of the first column of the first row, converted to type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method is an alias for <see cref="QueryScalar{T}(IDbConnection, string, object, CommandOptions{T})"/> and behaves identically.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var count = connection.ExecuteScalar&lt;long&gt;(
    ///     "SELECT COUNT(*) FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="ExecuteScalar{T}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static T ExecuteScalar<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options)
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
        return QueryScalarCore(connection, sql, parameters, options);
    }
}