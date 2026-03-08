using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query asynchronously and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the first entity mapped from the query results, or <see langword="null"/> if the query returns no results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>strict mapping mode</strong> by default. All public writable properties 
    /// on <typeparamref name="T"/> must have matching columns in the result set.
    /// </para>
    /// <para>
    /// Unlike <see cref="QueryFirstAsync{T}(IDbConnection, string, CancellationToken)"/>, this method returns <see langword="null"/> 
    /// instead of throwing an exception when no results are found.
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
    /// // Get first product or null if not found
    /// var product = await connection.QueryFirstOrDefaultAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE id = @Id", 
    ///     new { Id = 999 });
    /// 
    /// if (product == null)
    /// {
    ///     Console.WriteLine("Product not found");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/> or when a property has no matching column.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of provided parameters doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QuerySingleOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirstOrDefault{T}(IDbConnection, string)"/>
    public static ValueTask<T?> QueryFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultCoreAsync<T>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters asynchronously and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
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
    /// <returns>
    /// A task containing the first entity mapped from the query results, or <see langword="null"/> if the query returns no results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product in category or null
    /// var product = await connection.QueryFirstOrDefaultAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 999 });
    /// 
    /// if (product != null)
    /// {
    ///     Console.WriteLine($"Found: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/> or when a property has no matching column.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirstAsync{T}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<T?> QueryFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultCoreAsync<T>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with command options asynchronously and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
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
    /// <returns>
    /// A task containing the first entity mapped from the query results, or <see langword="null"/> if the query returns no results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = await connection.QueryFirstOrDefaultAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE id = @Id",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/> or when a property has no matching column.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T?> QueryFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultCoreAsync<T>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options asynchronously, returning the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
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
    /// <returns>
    /// A task containing the first entity mapped from the query results, or <see langword="null"/> if the query returns no results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var product = await connection.QueryFirstOrDefaultAsync&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE id = @Id",
    ///     new { Id = 999 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/> or when a property has no matching column.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<T?> QueryFirstOrDefaultAsync<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultCoreAsync<T>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
}