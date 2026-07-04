using System.Data;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and returns the single result mapped to an entity of type <typeparamref name="T"/> using partial mapping mode, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>
    /// The single entity mapped from the query results, or <see langword="null"/> if the query returns no results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>partial mapping mode</strong>. Only properties on <typeparamref name="T"/> that have 
    /// matching columns in the result set are mapped. Properties without matching columns are left with their default values.
    /// </para>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets. For a method that throws on empty results, 
    /// use <see cref="QueryPartialSingle{T}(IDbConnection, string)"/>.
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
    /// // Get single product or null if not found
    /// var product = connection.QueryPartialSingleOrDefault&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE id = @Id", 
    ///     new { Id = 999 });
    /// 
    /// if (product == null)
    /// {
    ///     Console.WriteLine("Product not found");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns more than one result.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of provided parameters doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialSingle{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryPartialFirstOrDefault{T}(IDbConnection, string)"/>
    /// <seealso cref="QuerySingleOrDefault{T}(IDbConnection, string)"/>
    public static T? QueryPartialSingleOrDefault<T>(this IDbConnection connection, string sql) where T : new()
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
        return QuerySingleOrDefaultCore<T>(connection, sql, null, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns the single result mapped to an entity of type <typeparamref name="T"/> using partial mapping mode, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>
    /// The single entity mapped from the query results, or <see langword="null"/> if the query returns no results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get single product by SKU or null if not found
    /// var product = connection.QueryPartialSingleOrDefault&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE sku = @Sku",
    ///     new { Sku = "WIDGET-999" });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns more than one result.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialSingleOrDefault{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryPartialSingle{T}(IDbConnection, string, object)"/>
    public static T? QueryPartialSingleOrDefault<T>(this IDbConnection connection, string sql, object parameters) where T : new()
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
        return QuerySingleOrDefaultCore<T>(connection, sql, parameters, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns the single result mapped to an entity of type <typeparamref name="T"/> using partial mapping mode, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>
    /// The single entity mapped from the query results, or <see langword="null"/> if the query returns no results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get single product with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = connection.QueryPartialSingleOrDefault&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE id = @Id",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns more than one result.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryPartialSingleOrDefault{T}(IDbConnection, string)"/>
    public static T? QueryPartialSingleOrDefault<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
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
        return QuerySingleOrDefaultCore<T>(connection, sql, null, options, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options, returning the single result mapped to an entity of type <typeparamref name="T"/> using partial mapping mode, 
    /// or <see langword="null"/> if no results are found.
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
    /// <returns>
    /// The single entity mapped from the query results, or <see langword="null"/> if the query returns no results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var product = connection.QueryPartialSingleOrDefault&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE id = @Id",
    ///     new { Id = 999 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns more than one result.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartialSingleOrDefault{T}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static T? QueryPartialSingleOrDefault<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
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
        return QuerySingleOrDefaultCore<T>(connection, sql, parameters, options, MappingMode.Projection);
    }
}