using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and returns the first result mapped to an entity of type <typeparamref name="T"/> using partial mapping mode,
    /// or <see langword="null"/> if the result set is empty.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>The first entity of type <typeparamref name="T"/> from the result set, or <see langword="null"/> if no results are found.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>partial mapping mode</strong>. Only properties on <typeparamref name="T"/> that have
    /// matching columns in the result set are mapped. Properties without matching columns are left with their default values.
    /// </para>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// For a method that throws when no results are found, use
    /// <see cref="QueryPartialFirst{T}(IDbConnection, string)"/>.
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
    /// // Get first product or null
    /// var product = connection.QueryPartialFirstOrDefault&lt;Product&gt;(
    ///     "SELECT TOP 1 id, name FROM products ORDER BY id");
    ///
    /// if (product != null)
    /// {
    ///     // Price will be 0 (default for decimal)
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="QueryPartialFirst{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryFirstOrDefault{T}(IDbConnection, string)"/>
    public static T? QueryPartialFirstOrDefault<T>(this IDbConnection connection, string sql) where T : new()
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
        return QueryFirstOrDefaultCore<T>(connection, sql, null, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns the first result mapped to an entity of type <typeparamref name="T"/> using partial mapping mode,
    /// or <see langword="null"/> if the result set is empty.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">An anonymous object or dictionary containing parameter values.</param>
    /// <returns>The first entity of type <typeparamref name="T"/> from the result set, or <see langword="null"/> if no results are found.</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>partial mapping mode</strong> — only properties with matching columns are mapped.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product in a category or null
    /// var product = connection.QueryPartialFirstOrDefault&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @CategoryId ORDER BY id",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialFirstOrDefault{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryPartialFirst{T}(IDbConnection, string, object)"/>
    public static T? QueryPartialFirstOrDefault<T>(this IDbConnection connection, string sql, object parameters) where T : new()
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
        return QueryFirstOrDefaultCore<T>(connection, sql, parameters, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns the first result mapped to an entity of type <typeparamref name="T"/> using partial mapping mode,
    /// or <see langword="null"/> if the result set is empty.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>The first entity of type <typeparamref name="T"/> from the result set, or <see langword="null"/> if no results are found.</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>partial mapping mode</strong> — only properties with matching columns are mapped.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = connection.QueryPartialFirstOrDefault&lt;Product&gt;(
    ///     "SELECT id, name FROM products ORDER BY id",
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <seealso cref="QueryPartialFirstOrDefault{T}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static T? QueryPartialFirstOrDefault<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
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
        return QueryFirstOrDefaultCore<T>(connection, sql, null, options, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and returns the first result mapped to an entity of type <typeparamref name="T"/> using partial mapping mode,
    /// or <see langword="null"/> if the result set is empty.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">An anonymous object or dictionary containing parameter values.</param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>The first entity of type <typeparamref name="T"/> from the result set, or <see langword="null"/> if no results are found.</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// Uses <strong>partial mapping mode</strong>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var product = connection.QueryPartialFirstOrDefault&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @CategoryId ORDER BY id",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialFirstOrDefault{T}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static T? QueryPartialFirstOrDefault<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
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
        return QueryFirstOrDefaultCore<T>(connection, sql, parameters, options, MappingMode.Projection);
    }
}
