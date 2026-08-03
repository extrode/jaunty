using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query that returns multiple result sets and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// Use this method when you need to execute a query that returns multiple result sets in a single database round-trip.
    /// The <see cref="GridReader"/> must be disposed after use, or use a <c>using</c> statement.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open while the <see cref="GridReader"/> is in use.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Basic multiple query
    /// using var grid = connection.QueryMultiple(
    ///     "SELECT * FROM products WHERE id = @Id; SELECT * FROM categories WHERE id = @CategoryId");
    /// 
    /// var product = grid.ReadFirst&lt;Product&gt;();
    /// var category = grid.ReadFirst&lt;Category&gt;();
    /// </code>
    /// </example>
    /// <seealso cref="QueryMultiple(IDbConnection, string, object)"/>
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, CancellationToken)"/>
    public static GridReader QueryMultiple(this IDbConnection connection, string sql)
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
        return ExecuteQueryMultiple(connection, sql, null, default);
    }

    /// <summary>
    /// Executes a SQL query with parameters that returns multiple result sets and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values. Property names must match 
    /// parameter names in the SQL (e.g., <c>@Id</c> matches property <c>Id</c>).
    /// </param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// The <see cref="GridReader"/> must be disposed after use.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection remains open while the <see cref="GridReader"/> is in use.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Multiple query with parameters
    /// using var grid = connection.QueryMultiple(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId; SELECT * FROM categories WHERE id = @CategoryId",
    ///     new { CategoryId = 5 });
    /// 
    /// var products = grid.Read&lt;Product&gt;().ToList();
    /// var category = grid.ReadFirst&lt;Category&gt;();
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryMultiple(IDbConnection, string)"/>
    /// <seealso cref="QueryMultiple(IDbConnection, string, object, CommandOptions)"/>
    public static GridReader QueryMultiple(this IDbConnection connection, string sql, object parameters)
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
        return ExecuteQueryMultiple(connection, sql, parameters, default);
    }

    /// <summary>
    /// Executes a SQL query with command options that returns multiple result sets and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute multiple queries within a transaction.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Multiple query with transaction
    /// using var tx = connection.BeginTransaction();
    /// using var grid = connection.QueryMultiple(
    ///     "SELECT * FROM products; SELECT * FROM categories",
    ///     CommandOptions.WithTransaction(tx));
    /// 
    /// var products = grid.Read&lt;Product&gt;().ToList();
    /// var categories = grid.Read&lt;Category&gt;().ToList();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryMultiple(IDbConnection, string)"/>
    public static GridReader QueryMultiple(this IDbConnection connection, string sql, CommandOptions options)
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
        return ExecuteQueryMultiple(connection, sql, null, options);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options that returns multiple result sets and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>A <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// using var grid = connection.QueryMultiple(
    ///     "SELECT * FROM products WHERE category_id = @CategoryId; SELECT * FROM categories WHERE id = @CategoryId",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// 
    /// var products = grid.Read&lt;Product&gt;().ToList();
    /// var category = grid.ReadFirst&lt;Category&gt;();
    /// </code>
    /// </example>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryMultiple(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static GridReader QueryMultiple(this IDbConnection connection, string sql, object parameters, CommandOptions options)
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
        return ExecuteQueryMultiple(connection, sql, parameters, options);
    }

    /// <summary>
    /// Executes a SQL query that returns multiple result sets and processes them with a callback action.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="reader">A callback action that receives the <see cref="GridReader"/> and processes the result sets.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="options">Optional command options.</param>
    /// <remarks>
    /// <para>
    /// This overload automatically disposes the <see cref="GridReader"/> after the callback completes.
    /// </para>
    /// <para>
    /// <strong>Do not return a deferred sequence from the callback.</strong> <see cref="GridReader.ReadStream{T}"/>,
    /// <see cref="GridReader.ReadPartialStream{T}"/> and their async twins return lazy iterators over the
    /// underlying reader, which this overload has already disposed by the time the caller enumerates what
    /// came back. Materialise inside the callback - <c>ToList()</c> - or use the overload that hands you the
    /// <see cref="GridReader"/> to dispose yourself.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Multiple query with callback
    /// connection.QueryMultiple(
    ///     "SELECT * FROM products; SELECT * FROM categories",
    ///     grid =>
    ///     {
    ///         var products = grid.Read&lt;Product&gt;().ToList();
    ///         var categories = grid.Read&lt;Category&gt;().ToList();
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="QueryMultiple(IDbConnection, string, object, CommandOptions)"/>
    public static void QueryMultiple(this IDbConnection connection, string sql, Action<GridReader> reader, object? parameters = null, CommandOptions options = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(reader);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        using GridReader gridReader = ExecuteQueryMultiple(connection, sql, parameters, options);
        reader(gridReader);
    }

    /// <summary>
    /// Executes a SQL query that returns multiple result sets and transforms them using a callback function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the callback function.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="reader">A callback function that receives the <see cref="GridReader"/> and returns a result.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="options">Optional command options.</param>
    /// <returns>The result of the callback function.</returns>
    /// <remarks>
    /// <para>
    /// This overload automatically disposes the <see cref="GridReader"/> after the callback completes.
    /// </para>
    /// <para>
    /// <strong>Do not return a deferred sequence from the callback.</strong> <see cref="GridReader.ReadStream{T}"/>,
    /// <see cref="GridReader.ReadPartialStream{T}"/> and their async twins return lazy iterators over the
    /// underlying reader, which this overload has already disposed by the time the caller enumerates what
    /// came back. Materialise inside the callback - <c>ToList()</c> - or use the overload that hands you the
    /// <see cref="GridReader"/> to dispose yourself.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Multiple query with transform
    /// var result = connection.QueryMultiple(
    ///     "SELECT * FROM products WHERE id = @Id; SELECT * FROM categories WHERE id = @CategoryId",
    ///     grid =>
    ///     {
    ///         var product = grid.ReadFirst&lt;Product&gt;();
    ///         var category = grid.ReadFirst&lt;Category&gt;();
    ///         return new ProductViewModel { Product = product, Category = category };
    ///     },
    ///     new { Id = 1, CategoryId = 5 });
    /// </code>
    /// </example>
    /// <seealso cref="QueryMultiple(IDbConnection, string, Action{GridReader}, object, CommandOptions)"/>
    public static TResult QueryMultiple<TResult>(this IDbConnection connection, string sql, Func<GridReader, TResult> reader, object? parameters = null, CommandOptions options = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(reader);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        using GridReader gridReader = ExecuteQueryMultiple(connection, sql, parameters, options);
        return reader(gridReader);
    }
}