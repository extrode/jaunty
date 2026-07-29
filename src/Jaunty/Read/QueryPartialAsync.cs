using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query asynchronously and returns all results mapped to entities of type <typeparamref name="T"/> using partial mapping mode.
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
    /// This method uses <strong>partial mapping mode</strong>. Only properties on <typeparamref name="T"/> that have 
    /// matching columns in the result set are mapped. Properties without matching columns are left with their default values.
    /// </para>
    /// <para>
    /// Use this method when:
    /// </para>
    /// <list type="bullet">
    /// <item><description>You only need to select specific columns from the database</description></item>
    /// <item><description>Your entity has more properties than the query returns</description></item>
    /// <item><description>You're creating DTOs or view models from query results</description></item>
    /// </list>
    /// <para>
    /// For strict mapping where all properties must have matching columns, use 
    /// <see cref="QueryAsync{T}(IDbConnection, string, CancellationToken)"/> instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }  // Not in query, will be default
    /// }
    /// 
    /// // Partial query - only Id and Name are mapped
    /// var products = await connection.QueryPartialAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products");
    /// 
    /// // Price will be 0 (default for decimal) for all products
    /// foreach (var product in products)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name} - ${product.Price}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="QueryAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryPartial{T}(IDbConnection, string)"/>
    public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
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
            : QueryCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters asynchronously and returns all results mapped to entities of type <typeparamref name="T"/> using partial mapping mode.
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
    /// <returns>A task containing a list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>partial mapping mode</strong> - only properties with matching columns are mapped.
    /// </para>
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
    /// </remarks>
    /// <example>
    /// <code>
    /// // Partial query with named parameters
    /// var products = await connection.QueryPartialAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with command options asynchronously and returns all results mapped to entities of type <typeparamref name="T"/> using partial mapping mode.
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
    /// var products = await connection.QueryPartialAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// 
    /// // With timeout
    /// var products = await connection.QueryPartialAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products",
    ///     CommandOptions&lt;Product&gt;.WithTimeout(30));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryPartialAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
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
            : QueryCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options asynchronously, returning all results mapped to entities of type <typeparamref name="T"/> using partial mapping mode.
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
    /// Uses <strong>partial mapping mode</strong> - only properties with matching columns are mapped.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var products = await connection.QueryPartialAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<List<T>> QueryPartialAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Projection, cancellationToken);
    }
}