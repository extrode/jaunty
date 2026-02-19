using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and returns all results mapped to entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>A list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>strict mapping mode</strong> by default. All public writable properties 
    /// on <typeparamref name="T"/> must have matching columns in the result set, or an 
    /// <see cref="InvalidOperationException"/> is thrown.
    /// </para>
    /// <para>
    /// For partial mapping where only existing columns are mapped, use 
    /// <see cref="QueryPartial{T}(IDbConnection, string)"/> instead.
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
    /// // Basic query
    /// var products = connection.Query&lt;Product&gt;("SELECT * FROM products");
    /// 
    /// // Query with WHERE clause
    /// var filtered = connection.Query&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE price &gt; @MinPrice", 
    ///     new { MinPrice = 100 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property on <typeparamref name="T"/> has no matching column in the result set.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the number of provided parameters doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryPartial{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryFirst{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryAsync{T}(IDbConnection, string)"/>
    public static List<T> Query<T>(this IDbConnection connection, string sql) where T : new()
    {
        return QueryCore<T>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns all results mapped to entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values. Property names must match 
    /// parameter names in the SQL (e.g., <c>@CategoryId</c> matches property <c>CategoryId</c>).
    /// </param>
    /// <returns>A list of all entities mapped from the query results.</returns>
    /// <remarks>
    /// <para>
    /// Supports both <strong>named parameters</strong> (via anonymous objects) and 
    /// <strong>positional parameters</strong> (via property arrays).
    /// </para>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Named parameters (recommended)
    /// var products = connection.Query&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId AND price &gt; @MinPrice",
    ///     new { CategoryId = 5, MinPrice = 100.00m });
    /// 
    /// // Positional parameters (values bound in order)
    /// var products = connection.Query&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @Id AND price &gt; @Min",
    ///     new { Id = 5, Min = 100.00m });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property on <typeparamref name="T"/> has no matching column in the result set.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL or parameter names don't match.
    /// </exception>
    /// <seealso cref="Query{T}(IDbConnection, string)"/>
    /// <seealso cref="QueryPartial{T}(IDbConnection, string, object)"/>
    public static List<T> Query<T>(this IDbConnection connection, string sql, object parameters) where T : new()
    {
        return QueryCore<T>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns all results mapped to entities of type <typeparamref name="T"/>.
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
    /// var products = connection.Query&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// tx.Commit();
    /// 
    /// // With timeout
    /// var products = connection.Query&lt;Product&gt;(
    ///     "SELECT * FROM products",
    ///     CommandOptions&lt;Product&gt;.WithTimeout(30));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property on <typeparamref name="T"/> has no matching column in the result set.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="Query{T}(IDbConnection, string)"/>
    public static List<T> Query<T>(this IDbConnection connection, string sql, CommandOptions<T> options) where T : new()
    {
        return QueryCore(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options, returning all results mapped to entities of type <typeparamref name="T"/>.
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
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns or an exception is thrown.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var products = connection.Query&lt;Product&gt;(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property on <typeparamref name="T"/> has no matching column in the result set.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="Query{T}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static List<T> Query<T>(this IDbConnection connection, string sql, object parameters, CommandOptions<T> options) where T : new()
    {
        return QueryCore(connection, sql, parameters, options, MappingMode.Strict);
    }
}
