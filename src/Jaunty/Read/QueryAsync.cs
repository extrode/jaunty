using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query asynchronously and returns all results mapped to entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>strict mapping mode</strong> by default. All public writable properties 
    /// on <typeparamref name="T"/> must have matching columns in the result set, or an 
    /// <see cref="InvalidOperationException"/> is thrown.
    /// </para>
    /// <para>
    /// For partial mapping where only existing columns are mapped, use 
    /// <see cref="QueryPartialAsync{T}(IDbConnection, string, CancellationToken)"/> instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    /// }
    /// 
    /// // Basic async query
    /// var products = await connection.QueryAsync&lt;Product&gt;("SELECT * FROM products");
    /// 
    /// // Query with cancellation token
    /// using var cts = new CancellationTokenSource();
    /// var products = await connection.QueryAsync&lt;Product&gt;(
    ///     "SELECT * FROM products", 
    ///     cts.Token);
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of provided parameters doesn't match the SQL.
    /// </exception>
    /// <seealso cref="Query{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryPartialAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
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
            : QueryCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters asynchronously and returns all results mapped to entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values. Property names must match 
    /// parameter names in the SQL (e.g., <c>@CategoryId</c> matches property <c>CategoryId</c>).
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// Parameters bind <strong>by name</strong> in every case. Three shapes are accepted:
    /// </para>
    /// <list type="bullet">
    /// <item><description>An <strong>anonymous object or POCO</strong> - each public property binds to
    /// the SQL parameter of the same name, so <c>new { CategoryId = 5 }</c> supplies
    /// <c>@CategoryId</c>.</description></item>
    /// <item><description>An <strong>IDictionary&lt;string, object?&gt;</strong> (including
    /// <see cref="System.Dynamic.ExpandoObject"/>) - each key binds to the SQL parameter of the same
    /// name.</description></item>
    /// <item><description>A <strong>single scalar</strong> (<c>int</c>, <c>string</c>, <c>Guid</c>,
    /// and so on) - bound to the one parameter the SQL names. This is a shorthand for the
    /// one-parameter case, not positional binding: it throws when the SQL names two or more distinct
    /// parameters, because a single value cannot say which is which.</description></item>
    /// </list>
    /// <para>
    /// <strong>There is no positional binding.</strong> An array is neither a dictionary nor a scalar,
    /// so it falls through to the by-name path and is reflected over as an ordinary object - which
    /// throws, listing the array's own members (<c>Length</c>, <c>Rank</c>, <c>SyncRoot</c>) as the
    /// available properties. Earlier versions of this documentation claimed otherwise (AUD-R26).
    /// </para>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Named parameters (recommended)
    /// var products = await connection.QueryAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId AND price &gt; @MinPrice",
    ///     new { CategoryId = 5, MinPrice = 100.00m });
    /// 
    /// // With cancellation
    /// using var cts = new CancellationTokenSource();
    /// var products = await connection.QueryAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     cts.Token);
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL or parameter names don't match.
    /// </exception>
    /// <seealso cref="QueryAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryPartialAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
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
            : QueryCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with command options asynchronously and returns all results mapped to entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions,
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout, or
    /// <see cref="CommandOptions{T}.WithMapper(Func{IDataReader, T})"/> for custom mapping.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need fine-grained control over query execution:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Transactions: <c>CommandOptions&lt;T&gt;.WithTransaction(tx)</c></description></item>
    /// <item><description>Timeout: <c>CommandOptions&lt;T&gt;.WithTimeout(30)</c></description></item>
    /// <item><description>Custom mapper: <c>CommandOptions&lt;T&gt;.WithMapper(MapProduct)</c></description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // With transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = await connection.QueryAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// 
    /// // With timeout
    /// var products = await connection.QueryAsync&lt;Product&gt;(
    ///     "SELECT * FROM products",
    ///     CommandOptions&lt;Product&gt;.WithTimeout(30));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
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
            : QueryCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options asynchronously, returning all results mapped to entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
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
    /// <returns>A task containing a list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns or an exception is thrown.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var products = await connection.QueryAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<List<T>> QueryAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
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
            : QueryCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
}