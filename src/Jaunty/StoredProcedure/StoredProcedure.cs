using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a stored procedure and returns the results as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <returns>A list of mapped entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method executes a stored procedure using strict mapping mode. All public writable properties 
    /// on <typeparamref name="T"/> must have matching columns in the result set.
    /// </para>
    /// <para>
    /// For stored procedures that accept parameters, use 
    /// <see cref="ExecuteStoredProcedure{T}(IDbConnection, string, object?)"/>.
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
    /// // Execute stored procedure that returns all products
    /// var products = connection.ExecuteStoredProcedure&lt;Product&gt;("GetAllProducts");
    /// 
    /// foreach (var product in products)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name} - ${product.Price}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedure{T}(IDbConnection, string, object?)"/>
    /// <seealso cref="ExecuteStoredProcedureAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedure<T>(connection, procedureName, (object?)null, default(CommandOptions<T>));
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns the results as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// Property names must match the stored procedure parameter names.
    /// </param>
    /// <returns>A list of mapped entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// This method executes a stored procedure using strict mapping mode.
    /// </para>
    /// <para>
    /// Parameter names in the anonymous object should match the stored procedure parameter names 
    /// (without the @ prefix). For example, use <c>new { CategoryId = 5 }</c> for a parameter named <c>@CategoryId</c>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure with parameters
    /// var products = connection.ExecuteStoredProcedure&lt;Product&gt;(
    ///     "GetProductsByCategory",
    ///     new { CategoryId = 5 });
    /// 
    /// foreach (var product in products)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedure{T}(IDbConnection, string)"/>
    /// <seealso cref="ExecuteStoredProcedure{T}(IDbConnection, string, object?, CommandOptions{T})"/>
    public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedure<T>(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options, returning the results as a list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>A list of mapped entities of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the stored procedure within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure with transaction
    /// using var tx = connection.BeginTransaction();
    /// var products = connection.ExecuteStoredProcedure(
    ///     "GetProductsByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// 
    /// // Execute stored procedure with timeout
    /// var products = connection.ExecuteStoredProcedure(
    ///     "GetProductsByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTimeout(60));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedure{T}(IDbConnection, string)"/>
    public static List<T> ExecuteStoredProcedure<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.Query<T>(procedureName, parameters!, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <returns>The first mapped entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// For a method that returns <see langword="null"/> instead of throwing, use 
    /// <see cref="ExecuteStoredProcedureFirstOrDefault{T}(IDbConnection, string)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product from stored procedure
    /// var product = connection.ExecuteStoredProcedureFirst&lt;Product&gt;("GetTopProduct");
    /// Console.WriteLine($"{product.Id}: {product.Name}");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored procedure returns no results.
    /// </exception>
    /// <seealso cref="ExecuteStoredProcedureFirst{T}(IDbConnection, string, object?)"/>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefault{T}(IDbConnection, string)"/>
    public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureFirst<T>(connection, procedureName, (object?)null, default(CommandOptions<T>));
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <returns>The first mapped entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product by category from stored procedure
    /// var product = connection.ExecuteStoredProcedureFirst&lt;Product&gt;(
    ///     "GetTopProductByCategory",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored procedure returns no results.
    /// </exception>
    /// <seealso cref="ExecuteStoredProcedureFirst{T}(IDbConnection, string)"/>
    /// <seealso cref="ExecuteStoredProcedureFirst{T}(IDbConnection, string, object?, CommandOptions{T})"/>
    public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureFirst<T>(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options, returning the first result mapped to an entity of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution.
    /// </param>
    /// <returns>The first mapped entity of type <typeparamref name="T"/>.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = connection.ExecuteStoredProcedureFirst(
    ///     "GetTopProductByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the stored procedure returns no results.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureFirst{T}(IDbConnection, string)"/>
    public static T ExecuteStoredProcedureFirst<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryFirst<T>(procedureName, parameters!, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <returns>The first mapped entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.</returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product or null from stored procedure
    /// var product = connection.ExecuteStoredProcedureFirstOrDefault&lt;Product&gt;("GetTopProduct");
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
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefault{T}(IDbConnection, string, object?)"/>
    /// <seealso cref="ExecuteStoredProcedureFirst{T}(IDbConnection, string)"/>
    public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureFirstOrDefault<T>(connection, procedureName, (object?)null, default(CommandOptions<T>));
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <returns>The first mapped entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.</returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product by category or null
    /// var product = connection.ExecuteStoredProcedureFirstOrDefault&lt;Product&gt;(
    ///     "GetTopProductByCategory",
    ///     new { CategoryId = 999 });
    /// 
    /// if (product != null)
    /// {
    ///     Console.WriteLine($"{product.Id}: {product.Name}");
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefault{T}(IDbConnection, string)"/>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefault{T}(IDbConnection, string, object?, CommandOptions{T})"/>
    public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName, object? parameters) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        return ExecuteStoredProcedureFirstOrDefault<T>(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options, returning the first result mapped to an entity of type <typeparamref name="T"/>, 
    /// or <see langword="null"/> if no results are found.
    /// </summary>
    /// <typeparam name="T">The entity type to map results to. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution.
    /// </param>
    /// <returns>The first mapped entity of type <typeparamref name="T"/>, or <see langword="null"/> if no results are found.</returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first product with transaction
    /// using var tx = connection.BeginTransaction();
    /// var product = connection.ExecuteStoredProcedureFirstOrDefault(
    ///     "GetTopProductByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureFirstOrDefault{T}(IDbConnection, string)"/>
    public static T? ExecuteStoredProcedureFirstOrDefault<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options) where T : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(procedureName)) throw new ArgumentException("Procedure name cannot be empty or whitespace.", nameof(procedureName));
#endif
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryFirstOrDefault<T>(procedureName, parameters!, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure and returns a scalar value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <returns>The scalar value returned by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that return a single value, such as aggregate functions 
    /// (COUNT, SUM, AVG, MIN, MAX) or computed values.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get total count from stored procedure
    /// var count = connection.ExecuteStoredProcedureScalar&lt;long&gt;("GetTotalProductCount");
    /// Console.WriteLine($"Total products: {count}");
    /// 
    /// // Get sum from stored procedure
    /// var total = connection.ExecuteStoredProcedureScalar&lt;decimal&gt;("GetTotalInventoryValue");
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureScalar{T}(IDbConnection, string, object?)"/>
    /// <seealso cref="ExecuteStoredProcedureScalarAsync{T}(IDbConnection, string, CancellationToken)"/>
    public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName)
    {
        return ExecuteStoredProcedureScalar<T>(connection, procedureName, (object?)null, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and returns a scalar value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <returns>The scalar value returned by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that return a single value with parameters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get count by category from stored procedure
    /// var count = connection.ExecuteStoredProcedureScalar&lt;long&gt;(
    ///     "GetProductCountByCategory",
    ///     new { CategoryId = 5 });
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureScalar{T}(IDbConnection, string)"/>
    /// <seealso cref="ExecuteStoredProcedureScalar{T}(IDbConnection, string, object?, CommandOptions{T})"/>
    public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, object? parameters)
    {
        return ExecuteStoredProcedureScalar<T>(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options, returning a scalar value of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The scalar type to return.</typeparam>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution.
    /// </param>
    /// <returns>The scalar value returned by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the stored procedure within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get count with transaction
    /// using var tx = connection.BeginTransaction();
    /// var count = connection.ExecuteStoredProcedureScalar(
    ///     "GetProductCountByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureScalar{T}(IDbConnection, string)"/>
    public static T ExecuteStoredProcedureScalar<T>(this IDbConnection connection, string procedureName, object? parameters, CommandOptions<T> options)
    {
        var spOptions = new CommandOptions<T>(options.Mapper, options.Transaction, options.CommandTimeout, CommandType.StoredProcedure);
        return connection.QueryScalar<T>(procedureName, parameters!, spOptions);
    }

    /// <summary>
    /// Executes a stored procedure that does not return results (INSERT, UPDATE, DELETE) and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <returns>The number of rows affected by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that perform data modification operations without returning result sets.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure that deletes old products
    /// int deleted = connection.ExecuteStoredProcedureNonQuery("DeleteOldProducts");
    /// Console.WriteLine($"Deleted {deleted} products");
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureNonQuery(IDbConnection, string, object?, CommandOptions)"/>
    /// <seealso cref="ExecuteStoredProcedureNonQueryAsync(IDbConnection, string, CancellationToken)"/>
    public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName)
    {
        return ExecuteStoredProcedureNonQuery(connection, procedureName, (object?)null, default(CommandOptions));
    }

    /// <summary>
    /// Executes a stored procedure with parameters that does not return results and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <returns>The number of rows affected by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this method for stored procedures that perform data modification operations with parameters.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure that deletes products by category
    /// int deleted = connection.ExecuteStoredProcedureNonQuery(
    ///     "DeleteProductsByCategory",
    ///     new { CategoryId = 5 });
    /// Console.WriteLine($"Deleted {deleted} products");
    /// </code>
    /// </example>
    /// <seealso cref="ExecuteStoredProcedureNonQuery(IDbConnection, string)"/>
    /// <seealso cref="ExecuteStoredProcedureNonQuery(IDbConnection, string, object?, CommandOptions)"/>
    public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, object? parameters)
    {
        return ExecuteStoredProcedureNonQuery(connection, procedureName, parameters, default);
    }

    /// <summary>
    /// Executes a stored procedure with parameters and options that does not return results and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the stored procedure against.</param>
    /// <param name="procedureName">The name of the stored procedure to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values to pass to the stored procedure.
    /// </param>
    /// <param name="options">
    /// Command options for configuring the stored procedure execution.
    /// </param>
    /// <returns>The number of rows affected by the stored procedure.</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the stored procedure within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Execute stored procedure with transaction
    /// using var tx = connection.BeginTransaction();
    /// int deleted = connection.ExecuteStoredProcedureNonQuery(
    ///     "DeleteProductsByCategory",
    ///     new { CategoryId = 5 },
    ///     CommandOptions.WithTransaction(tx));
    /// tx.Commit();
    /// </code>
    /// </example>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="ExecuteStoredProcedureNonQuery(IDbConnection, string)"/>
    public static int ExecuteStoredProcedureNonQuery(this IDbConnection connection, string procedureName, object? parameters, CommandOptions options)
    {
        return ExecuteNonQueryCore(connection, procedureName, parameters, options, CommandType.StoredProcedure);
    }
}