using System.Data;
using System.Data.Common;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Asynchronously executes a stored procedure and returns the results as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of mapped entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method executes a stored procedure using strict mapping mode. All public writable properties 
    /// on <typeparamref name="T"/> must have matching columns in the result set.
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
    /// // Async execute stored procedure
    /// var products = await connection.ExecuteStoredProcedureAsync&lt;Product&gt;("GetAllProducts");
    /// 
    /// foreach (var product in products)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name} - ${product.Price}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureAsync{T}(IDbConnection, string, object?, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedure{T}(IDbConnection, string)"/>
    public static ValueTask<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureAsync<T>(connection, procedureName, (object?)null, default(CommandOptions<T>), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and returns the results as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of mapped entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// Parameter names in the anonymous object should match the stored procedure parameter names.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure with parameters
    /// var products = await connection.ExecuteStoredProcedureAsync&lt;Product&gt;(
    ///     "GetProductsByCategory",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureAsync{T}(IDbConnection, string, object?, CommandOptions{T}, CancellationToken)"/>
    public static ValueTask<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureAsync<T>(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options, returning the results as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of mapped entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the stored procedure within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = await connection.ExecuteStoredProcedureAsync&lt;Product&gt;(
    ///     "GetProductsByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<List<T>> ExecuteStoredProcedureAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure, options.ExpectedRowCount);
        return parameters is null
            ? connection.QueryAsync<T>(procedureName, spOptions, cancellationToken)
            : connection.QueryAsync<T>(procedureName, parameters, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the first mapped entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// For a method that returns <see langword="null"/> instead of throwing, use 
    /// <see cref="ExecuteStoredProcedureFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get first product from stored procedure
    /// var product = await connection.ExecuteStoredProcedureFirstAsync&lt;Product&gt;("GetTopProduct");
    /// Console.WriteLine($"{product.Id}: {product.Name}");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored procedure returns no results.
    /// </exception>
    /// <seealso cref="ExecuteStoredProcedureFirstAsync{T}(IDbConnection, string, object?, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureFirstAsync<T>(connection, procedureName, (object?)null, default(CommandOptions<T>), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the first mapped entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get first product by category from stored procedure
    /// var product = await connection.ExecuteStoredProcedureFirstAsync&lt;Product&gt;(
    ///     "GetTopProductByCategory",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored procedure returns no results.
    /// </exception>
    /// <seealso cref="ExecuteStoredProcedureFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureFirstAsync{T}(IDbConnection, string, object?, CommandOptions{T}, CancellationToken)"/>
    public static ValueTask<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureFirstAsync<T>(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options, returning the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the first mapped entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get first product with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = await connection.ExecuteStoredProcedureFirstAsync&lt;Product&gt;(
    ///     "GetTopProductByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored procedure returns no results.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> ExecuteStoredProcedureFirstAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure, options.ExpectedRowCount);
        return parameters is null
            ? connection.QueryFirstAsync<T>(procedureName, spOptions, cancellationToken)
            : connection.QueryFirstAsync<T>(procedureName, parameters, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the first mapped entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get first product or null from stored procedure
    /// var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync&lt;Product&gt;("GetTopProduct");
    /// 
    /// if (product != null)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// else
    /// {
    ///     Console.WriteLine("No products found");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefaultAsync{T}(IDbConnection, string, object?, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureFirstAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureFirstOrDefaultAsync<T>(connection, procedureName, (object?)null, default(CommandOptions<T>), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the first mapped entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get first product by category or null
    /// var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync&lt;Product&gt;(
    ///     "GetTopProductByCategory",
    ///     new { CategoryId = 999 });
    /// 
    /// if (product != null)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefaultAsync{T}(IDbConnection, string, object?, CommandOptions{T}, CancellationToken)"/>
    public static ValueTask<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureFirstOrDefaultAsync<T>(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options, returning the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing the first mapped entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get first product with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = await connection.ExecuteStoredProcedureFirstOrDefaultAsync&lt;Product&gt;(
    ///     "GetTopProductByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;Product&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefaultAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure, options.ExpectedRowCount);
        return parameters is null
            ? connection.QueryFirstOrDefaultAsync<T>(procedureName, spOptions, cancellationToken)
            : connection.QueryFirstOrDefaultAsync<T>(procedureName, parameters, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure and returns a scalar value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the scalar value returned by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that return a single value, such as aggregate functions.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get total count from stored procedure
    /// var count = await connection.ExecuteStoredProcedureScalarAsync&lt;long&gt;("GetTotalProductCount");
    /// Console.WriteLine($"Total products: {count}");
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureScalarAsync{T}(IDbConnection, string, object?, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureScalar{T}(IDbConnection, string)"/>
    public static ValueTask<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureScalarAsync<T>(connection, procedureName, (object?)null, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and returns a scalar value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the scalar value returned by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that return a single value with parameters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get count by category from stored procedure
    /// var count = await connection.ExecuteStoredProcedureScalarAsync&lt;long&gt;(
    ///     "GetProductCountByCategory",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureScalarAsync{T}(IDbConnection, string, object?, CommandOptions{T}, CancellationToken)"/>
    public static ValueTask<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureScalarAsync<T>(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options, returning a scalar value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the scalar value returned by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the stored procedure within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async get count with transaction
    /// using var tx = connection.BeginTransaction();
    /// var count = await connection.ExecuteStoredProcedureScalarAsync&lt;long&gt;(
    ///     "GetProductCountByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions&lt;long&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<T> ExecuteStoredProcedureScalarAsync<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure, options.ExpectedRowCount);
        return parameters is null
            ? connection.QueryScalarAsync<T>(procedureName, spOptions, cancellationToken)
            : connection.QueryScalarAsync<T>(procedureName, parameters, spOptions, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure that does not return results (INSERT, UPDATE, DELETE) and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows affected by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that perform data modification operations without returning result sets.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure that deletes old products
    /// int deleted = await connection.ExecuteStoredProcedureNonQueryAsync("DeleteOldProducts");
    /// Console.WriteLine($"Deleted {deleted} products");
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureNonQueryAsync(IDbConnection, string, object?, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureNonQuery(IDbConnection, string)"/>
    public static ValueTask<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureNonQueryAsync(connection, procedureName, (object?)null, default(CommandOptions), cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters that does not return results and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows affected by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that perform data modification operations with parameters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure that deletes products by category
    /// int deleted = await connection.ExecuteStoredProcedureNonQueryAsync(
    ///     "DeleteProductsByCategory",
    ///     new { CategoryId = 5 });
    /// Console.WriteLine($"Deleted {deleted} products");
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureNonQueryAsync(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="ExecuteStoredProcedureNonQueryAsync(IDbConnection, string, object?, CommandOptions, CancellationToken)"/>
    public static ValueTask<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, object? parameters, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureNonQueryAsync(connection, procedureName, parameters, default, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a stored procedure with parameters and options that does not return results and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the stored procedure against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing the number of rows affected by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the stored procedure within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async execute stored procedure with transaction
    /// using var tx = connection.BeginTransaction();
    /// int deleted = await connection.ExecuteStoredProcedureNonQueryAsync(
    ///     "DeleteProductsByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureNonQueryAsync(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<int> ExecuteStoredProcedureNonQueryAsync(this IDbConnection connection, string procedureName, object? parameters, CommandOptions options, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : ExecuteNonQueryCoreAsync(dbConnection, procedureName, parameters, options, CommandType.StoredProcedure, cancellationToken);
    }
}