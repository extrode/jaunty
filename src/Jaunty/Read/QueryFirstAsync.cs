using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query asynchronously and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the first entity mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>strict mapping mode</strong> by default. All public writable properties 
    /// on <typeparamref name="T"/> must have matching columns in the result set.
    /// </para>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong> 
    /// For a method that returns <see langword="null"/> instead of throwing, use 
    /// <see cref="QueryFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>.
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
    /// // Get first product by ID
    /// var product = await connection.QueryFirstAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE id = @Id", 
    ///     new { Id = 1 });
    /// 
    /// // Get first product ordered by price
    /// var cheapest = await connection.QueryFirstAsync&lt;Product&gt;(
    ///     "SELECT * FROM products ORDER BY price ASC");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results, 
    /// or when a property has no matching column.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of provided parameters doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QuerySingleAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirst{T}(IDbConnection, string)"/>
    public static ValueTask<T> QueryFirstAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
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
            : QueryFirstCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters asynchronously and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the first entity mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product in category
    /// var product = await connection.QueryFirstAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results, 
    /// or when a property has no matching column.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirstOrDefaultAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T> QueryFirstAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
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
            : QueryFirstCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with command options asynchronously and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
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
    /// <returns>A task containing the first entity mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = await connection.QueryFirstAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE id = @Id",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results, 
    /// or when a property has no matching column.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> QueryFirstAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
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
            : QueryFirstCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options asynchronously, returning the first result mapped to an entity of type <typeparamref name="T"/>.
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
    /// <returns>A task containing the first entity mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var product = await connection.QueryFirstAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE id = @Id",
    ///     new { Id = 1 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results, 
    /// or when a property has no matching column.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<T> QueryFirstAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
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
            : QueryFirstCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
}