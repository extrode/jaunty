using System.Data;

using Jaunty.Core;
using Jaunty.Configuration;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and returns all results mapped to entities of type <typeparamref name="T"/> using partial mapping mode.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>A list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>partial mapping mode</strong> (also known as projection mode). Only properties 
    /// on <typeparamref name="T"/> that have matching columns in the result set are mapped. Properties without 
    /// matching columns are left with their default values.
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
    /// <see cref="Query{T}(IDbConnection, string)"/> instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Product
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    ///     public decimal Price { get; set; }
    ///     public string Description { get; set; }  // Not in query
    /// }
    /// 
    /// // Partial query - only Id and Name are mapped
    /// var products = connection.QueryPartial&lt;Product&gt;(
    ///     "SELECT id, name FROM products");
    /// 
    /// // Description will be null (default for reference types)
    /// foreach (var product in products)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// 
    /// // Useful for DTOs
    /// public class ProductSummary
    /// {
    ///     public int Id { get; set; }
    ///     public string Name { get; set; }
    /// }
    /// 
    /// var summaries = connection.QueryPartial&lt;ProductSummary&gt;(
    ///     "SELECT id, name FROM products");
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of provided parameters doesn't match the SQL.
    /// </exception>
    /// <seealso cref="Query{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryPartialAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryPartialFirst{T}(IDbConnection, string)"/>
    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql) where T : new()
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
        return QueryCore<T>(connection, sql, null, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns all results mapped to entities of type <typeparamref name="T"/> using partial mapping mode.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>A list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>partial mapping mode</strong> - only properties with matching columns are mapped.
    /// </para>
    /// <para>
    /// Supports both <strong>named parameters</strong> (via anonymous objects) and 
    /// <strong>positional parameters</strong> (via property arrays).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Partial query with named parameters
    /// var products = connection.QueryPartial&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 });
    /// 
    /// // Partial query with positional parameters
    /// var products = connection.QueryPartial&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @Id",
    ///     5);
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartial{T}(IDbConnection, string)"/>
    /// <seealso cref="Query{T}(IDbConnection, string, object)"/>
    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters) where T : new()
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
        return QueryCore<T>(connection, sql, parameters, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns all results mapped to entities of type <typeparamref name="T"/> using partial mapping mode.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions,
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout, or
    /// <see cref="CommandOptions{T}.WithMapper(Func{IDataReader, T})"/> for custom mapping.
    /// </param>
    /// <returns>A list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>partial mapping mode</strong> - only properties with matching columns are mapped.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Partial query with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = connection.QueryPartial&lt;Product&gt;(
    ///     "SELECT id, name FROM products",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// 
    /// // Partial query with timeout
    /// var products = connection.QueryPartial&lt;Product&gt;(
    ///     "SELECT id, name FROM products",
    ///     CommandOptions&lt;Product&gt;.WithTimeout(30));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryPartial{T}(IDbConnection, string)"/>
    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
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
        return QueryCore<T>(connection, sql, null, options, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options, returning all results mapped to entities of type <typeparamref name="T"/> using partial mapping mode.
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
    /// <returns>A list of all entities mapped from the query results.</returns>
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
    /// var products = connection.QueryPartial&lt;Product&gt;(
    ///     "SELECT id, name FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartial{T}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static List<T> QueryPartial<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
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
        return QueryCore<T>(connection, sql, parameters, options, MappingMode.Projection);
    }
}