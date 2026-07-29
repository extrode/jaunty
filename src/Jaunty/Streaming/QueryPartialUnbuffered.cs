using System.Data;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and streams the results as entities of type <typeparamref name="T"/> using partial mapping mode (unbuffered).
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>An enumerable of entities of type <typeparamref name="T"/> that streams results from the database.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>partial mapping mode</strong>. Only properties on <typeparamref name="T"/> that have 
    /// matching columns in the result set are mapped. Properties without matching columns are left with their default values.
    /// </para>
    /// <para>
    /// This overload is functionally identical to <see cref="QueryPartialStream{T}(IDbConnection, string)"/>:
    /// results are streamed and not buffered in memory. It exists as an alias for callers who prefer the
    /// "unbuffered" naming to describe the streaming behavior.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open until the enumeration completes.
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
    /// // Unbuffered stream - most memory efficient for large result sets
    /// foreach (var product in connection.QueryPartialUnbuffered&lt;Product&gt;(
    ///     "SELECT id, name FROM products"))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="QueryPartialUnbuffered{T}(IDbConnection, string, object)"/>
    /// <seealso cref="QueryPartialStream{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryStream{T}(IDbConnection, string)"/>
    public static IEnumerable<T> QueryPartialUnbuffered<T>(this IDbConnection connection, string sql) where T : new()
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
        return QueryStreamCore<T>(connection, sql, null, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and streams the results as entities of type <typeparamref name="T"/> using partial mapping mode (unbuffered).
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
    /// Uses <strong>partial mapping mode</strong> - only properties with matching columns are mapped.
    /// </para>
    /// <para>
    /// This overload is functionally identical to
    /// <see cref="QueryPartialStream{T}(IDbConnection, string, object)"/> - it streams results without
    /// buffering them in memory.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Unbuffered stream with parameters
    /// foreach (var product in connection.QueryPartialUnbuffered&lt;Product&gt;(
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
    /// <seealso cref="QueryPartialUnbuffered{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryPartialUnbuffered{T}(IDbConnection, string, object, CommandOptions{T})"/>
    public static IEnumerable<T> QueryPartialUnbuffered<T>(this IDbConnection connection, string sql, object parameters) where T : new()
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
        return QueryStreamCore<T>(connection, sql, parameters, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with command options and streams the results as entities of type <typeparamref name="T"/> using partial mapping mode (unbuffered).
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
    /// This overload is functionally identical to
    /// <see cref="QueryPartialStream{T}(IDbConnection, string, CommandOptions{T})"/> - it streams results
    /// without buffering them in memory.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Unbuffered stream with transaction
    /// using var tx = connection.BeginTransaction();
    /// foreach (var product in connection.QueryPartialUnbuffered&lt;Product&gt;(
    ///     "SELECT id, name FROM products",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx)))
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryPartialUnbuffered{T}(IDbConnection, string)"/>
    public static IEnumerable<T> QueryPartialUnbuffered<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
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
        return QueryStreamCore<T>(connection, sql, null, options, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and streams the results as entities of type <typeparamref name="T"/> using partial mapping mode (unbuffered).
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
    /// This overload is functionally identical to
    /// <see cref="QueryPartialStream{T}(IDbConnection, string, object, CommandOptions{T})"/> - it streams
    /// results without buffering them in memory.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Unbuffered stream with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// foreach (var product in connection.QueryPartialUnbuffered&lt;Product&gt;(
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
    /// <seealso cref="QueryPartialUnbuffered{T}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static IEnumerable<T> QueryPartialUnbuffered<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
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
        return QueryStreamCore<T>(connection, sql, parameters, options, MappingMode.Projection);
    }
}