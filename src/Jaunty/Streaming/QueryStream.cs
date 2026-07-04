using System.Data;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and streams the results as entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>An enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>strict mapping mode</strong>. All public writable properties on 
    /// <typeparamref name="T"/> must have matching columns in the result set.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open until the enumeration completes. 
    /// Use a <c>using</c> statement or ensure you fully enumerate the results before closing the connection.
    /// </para>
    /// <para>
    /// Streaming is memory-efficient for large result sets as it doesn't buffer all results in memory.
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
    /// // Stream all products (connection stays open during enumeration)
    /// foreach (var product in connection.QueryStream&lt;Product&gt;("SELECT * FROM products"))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name} - ${product.Price}");
    /// }
    /// 
    /// // Or use LINQ with ToList() to buffer results and close the connection
    /// var products = connection.QueryStream&lt;Product&gt;("SELECT * FROM products").ToList();
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryStream{T}(IDbConnection, string, object)"/>
    /// <seealso cref="Query{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryPartialStream{T}(IDbConnection, string)"/>
    public static IEnumerable<T> QueryStream<T>(this IDbConnection connection, string sql) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return QueryStreamCore<T>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and streams the results as entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>An enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open until the enumeration completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stream products by category
    /// foreach (var product in connection.QueryStream&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 }))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryStream{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryStream{T}(IDbConnection, string, object, CommandOptions{T})"/>
    public static IEnumerable<T> QueryStream<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return QueryStreamCore<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with command options and streams the results as entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>An enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the query within a transaction or with a specific timeout.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open until the enumeration completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stream products with transaction
    /// using var tx = connection.BeginTransaction();
    /// foreach (var product in connection.QueryStream&lt;Product&gt;(
    ///     "SELECT * FROM products",
    ///     CommandOptions.WithTransaction(tx)))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryStream{T}(IDbConnection, string)"/>
    public static IEnumerable<T> QueryStream<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return QueryStreamCore<T>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and streams the results as entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>An enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open until the enumeration completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stream products with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// foreach (var product in connection.QueryStream&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx)))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryStream{T}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static IEnumerable<T> QueryStream<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return QueryStreamCore<T>(connection, sql, parameters, options, MappingMode.Strict);
    }
}