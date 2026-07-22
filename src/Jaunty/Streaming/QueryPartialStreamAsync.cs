using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously executes a SQL query and streams the results as entities of type <typeparamref name="T"/> using partial mapping mode.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>An async enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>partial mapping mode</strong>. Only properties on <typeparamref name="T"/> that have 
    /// matching columns in the result set are mapped. Properties without matching columns are left with their default values.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open until the async enumeration completes.
    /// Use <c>await foreach</c> to properly enumerate the results.
    /// </para>
    /// <para>
    /// <strong>Interceptor gap:</strong> streamed commands honor <see cref="CommandOptions{T}.CommandType"/>
    /// and the simple <see cref="global::Jaunty.Configuration.JauntyConfig.Logger"/> callback, the same as
    /// buffered queries, but they do NOT currently pass through the registered
    /// <see cref="global::Jaunty.Interceptors.ICommandInterceptor"/> pipeline. Wiring pipeline interceptors into
    /// a streaming path would require materializing the entire result set before the "command executed"
    /// hook could fire, which would defeat the purpose of streaming, so this is intentionally left
    /// unwired for now. Callers relying on interceptor-based auditing should not assume streamed
    /// queries (<c>QueryStream</c>, <c>QueryPartialStream</c>, <c>QueryPartialUnbuffered</c>, and their
    /// async equivalents) are observed by their interceptors.
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
    /// // Async stream products with partial columns
    /// await foreach (var product in connection.QueryPartialStreamAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products"))
    /// {
    ///     // Price will be 0 (default for decimal)
    ///     Console.WriteLine($"{product.Id}: {product.Name} - ${product.Price}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="QueryPartialStreamAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    /// <seealso cref="QueryStreamAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryPartialAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static IAsyncEnumerable<T> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
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
            : QueryStreamCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and streams the results as entities of type <typeparamref name="T"/> using partial mapping mode.
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
    /// <returns>An async enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>partial mapping mode</strong> - only properties with matching columns are mapped.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async stream products by category with partial columns
    /// await foreach (var product in connection.QueryPartialStreamAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 }))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialStreamAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryPartialStreamAsync{T}(IDbConnection, string, object, CommandOptions{T}, CancellationToken)"/>
    public static IAsyncEnumerable<T> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
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
            : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with command options and streams the results as entities of type <typeparamref name="T"/> using partial mapping mode.
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
    /// <returns>An async enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the query within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async stream products with transaction
    /// using var tx = connection.BeginTransaction();
    /// await foreach (var product in connection.QueryPartialStreamAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx)))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryPartialStreamAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static IAsyncEnumerable<T> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
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
            : QueryStreamCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Projection, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and command options and streams the results as entities of type <typeparamref name="T"/> using partial mapping mode.
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
    /// <returns>An async enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async stream products with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// await foreach (var product in connection.QueryPartialStreamAsync&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx)))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialStreamAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static IAsyncEnumerable<T> QueryPartialStreamAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
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
            : QueryStreamCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Projection, cancellationToken);
    }
}