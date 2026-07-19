using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query that returns multiple result sets asynchronously and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
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
    /// // Basic async multiple query
    /// using var grid = await connection.QueryMultipleAsync(
    ///     "SELECT * FROM products WHERE id = @Id; SELECT * FROM categories WHERE id = @CategoryId");
    /// 
    /// var product = grid.ReadFirst&lt;Product&gt;();
    /// var category = grid.ReadFirst&lt;Category&gt;();
    /// </code>
    /// </example>
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, object, CancellationToken)"/>
    /// <seealso cref="QueryMultiple(IDbConnection, string)"/>
    public static ValueTask<GridReader> QueryMultipleAsync(this IDbConnection connection, string sql, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteQueryMultipleAsync(dbConnection, sql, null, default, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters that returns multiple result sets asynchronously and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// The <see cref="GridReader"/> must be disposed after use.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async multiple query with parameters
    /// using var grid = await connection.QueryMultipleAsync(
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
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, object, CommandOptions, CancellationToken)"/>
    public static ValueTask<GridReader> QueryMultipleAsync(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteQueryMultipleAsync(dbConnection, sql, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with command options that returns multiple result sets asynchronously and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute multiple queries within a transaction.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async multiple query with transaction
    /// using var tx = connection.BeginTransaction();
    /// using var grid = await connection.QueryMultipleAsync(
    ///     "SELECT * FROM products; SELECT * FROM categories",
    ///     CommandOptions.WithTransaction(tx));
    /// 
    /// var products = grid.Read&lt;Product&gt;().ToList();
    /// var categories = grid.Read&lt;Category&gt;().ToList();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<GridReader> QueryMultipleAsync(this IDbConnection connection, string sql, CommandOptions options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteQueryMultipleAsync(dbConnection, sql, null, options, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options that returns multiple result sets asynchronously and returns a <see cref="GridReader"/> to read them.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a <see cref="GridReader"/> that can read multiple result sets from the query.</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full async example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// using var grid = await connection.QueryMultipleAsync(
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
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<GridReader> QueryMultipleAsync(this IDbConnection connection, string sql, object parameters, CommandOptions options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteQueryMultipleAsync(dbConnection, sql, parameters, options, cancellationToken);
    }

    /// <summary>
    /// Executes a SQL query that returns multiple result sets asynchronously and processes them with a callback action.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="reader">A callback action that receives the <see cref="GridReader"/> and processes the result sets.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="options">Optional command options.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the callback finishes processing.</returns>
    /// <remarks>
    /// <para>
    /// This overload automatically disposes the <see cref="GridReader"/> after the callback completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async multiple query with callback
    /// await connection.QueryMultipleAsync(
    ///     "SELECT * FROM products; SELECT * FROM categories",
    ///     grid =>
    ///     {
    ///         var products = grid.Read&lt;Product&gt;().ToList();
    ///         var categories = grid.Read&lt;Category&gt;().ToList();
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, object, CommandOptions, CancellationToken)"/>
    public static async ValueTask QueryMultipleAsync(this IDbConnection connection, string sql, Action<GridReader> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(reader);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        using GridReader gridReader = await ExecuteQueryMultipleAsync(dbConnection, sql, parameters, options, cancellationToken).ConfigureAwait(false);
        reader(gridReader);
    }

    /// <summary>
    /// Executes a SQL query that returns multiple result sets asynchronously and processes them with an async callback function.
    /// </summary>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="reader">An async callback function that receives the <see cref="GridReader"/> and processes the result sets.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="options">Optional command options.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that completes when the callback finishes processing.</returns>
    /// <remarks>
    /// <para>
    /// This overload supports async operations within the callback and automatically disposes the <see cref="GridReader"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async multiple query with async callback
    /// await connection.QueryMultipleAsync(
    ///     "SELECT * FROM products; SELECT * FROM categories",
    ///     async grid =>
    ///     {
    ///         var products = grid.Read&lt;Product&gt;().ToList();
    ///         var categories = grid.Read&lt;Category&gt;().ToList();
    ///         await SaveToCacheAsync(products, categories);
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, Action{GridReader}, object, CommandOptions, CancellationToken)"/>
    public static async ValueTask QueryMultipleAsync(this IDbConnection connection, string sql, Func<GridReader, Task> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(reader);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        using GridReader gridReader = await ExecuteQueryMultipleAsync(dbConnection, sql, parameters, options, cancellationToken).ConfigureAwait(false);
        await reader(gridReader).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a SQL query that returns multiple result sets asynchronously and transforms them using a callback function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the callback function.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="reader">A callback function that receives the <see cref="GridReader"/> and returns a result.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="options">Optional command options.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of the callback function.</returns>
    /// <remarks>
    /// <para>
    /// This overload automatically disposes the <see cref="GridReader"/> after the callback completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async multiple query with transform
    /// var result = await connection.QueryMultipleAsync(
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
    /// <seealso cref="QueryMultipleAsync(IDbConnection, string, Func{GridReader, Task}, object, CommandOptions, CancellationToken)"/>
    public static async ValueTask<TResult> QueryMultipleAsync<TResult>(this IDbConnection connection, string sql, Func<GridReader, TResult> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(reader);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        using GridReader gridReader = await ExecuteQueryMultipleAsync(dbConnection, sql, parameters, options, cancellationToken).ConfigureAwait(false);
        return reader(gridReader);
    }

    /// <summary>
    /// Executes a SQL query that returns multiple result sets asynchronously and transforms them using an async callback function.
    /// </summary>
    /// <typeparam name="TResult">The type of the result returned by the callback function.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Can contain multiple SELECT statements separated by semicolons.</param>
    /// <param name="reader">An async callback function that receives the <see cref="GridReader"/> and returns a result.</param>
    /// <param name="parameters">Optional parameters for the query.</param>
    /// <param name="options">Optional command options.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of the callback function.</returns>
    /// <remarks>
    /// <para>
    /// This overload supports async operations within the callback and automatically disposes the <see cref="GridReader"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async multiple query with async transform
    /// var result = await connection.QueryMultipleAsync(
    ///     "SELECT * FROM products WHERE id = @Id; SELECT * FROM categories WHERE id = @CategoryId",
    ///     async grid =>
    ///     {
    ///         var product = grid.ReadFirst&lt;Product&gt;();
    ///         var category = grid.ReadFirst&lt;Category&gt;();
    ///         return await BuildViewModelAsync(product, category);
    ///     },
    ///     new { Id = 1, CategoryId = 5 });
    /// </code>
    /// </example>
    /// <seealso cref="QueryMultipleAsync{TResult}(IDbConnection, string, Func{GridReader, Task{TResult}}, object?, CommandOptions, CancellationToken)"/>
    public static async ValueTask<TResult> QueryMultipleAsync<TResult>(this IDbConnection connection, string sql, Func<GridReader, Task<TResult>> reader, object? parameters = null, CommandOptions options = default, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(reader);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (reader is null) throw new ArgumentNullException(nameof(reader));
#endif
        if (connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        using GridReader gridReader = await ExecuteQueryMultipleAsync(dbConnection, sql, parameters, options, cancellationToken).ConfigureAwait(false);
        return await reader(gridReader).ConfigureAwait(false);
    }
}