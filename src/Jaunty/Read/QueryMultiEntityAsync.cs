using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Internals.Read;

namespace Jaunty;

public static partial class Jaunty
{
    #region Consistent Multi-Entity QueryAsync APIs (Following same pattern as regular QueryAsync APIs)

    /// <summary>
    /// Asynchronously executes a SQL query and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">
    /// The SQL query to execute. Should return columns that can be mapped to both <typeparamref name="T1"/> 
    /// and <typeparamref name="T2"/>. Use SQL aliases to disambiguate columns with the same name.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of tuples with mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>strict mapping mode</strong>. All public writable properties on both 
    /// <typeparamref name="T1"/> and <typeparamref name="T2"/> must have matching columns in the result set.
    /// </para>
    /// <para>
    /// Columns are matched to entity properties using case-insensitive name matching. If a column name 
    /// matches properties in both types, <typeparamref name="T1"/> has priority.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async query for orders with their customers
    /// var results = await connection.QueryAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id");
    /// 
    /// foreach (var (order, customer) in results)
    /// {
    ///     Console.WriteLine($"Order {order.OrderId} by {customer.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryAsync{T1, T2}(IDbConnection, string, object, CancellationToken)"/>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string)"/>
    public static ValueTask<List<(T1, T2)>> QueryAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a list of tuples with mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async query for orders with their customers for a specific customer
    /// var results = await connection.QueryAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryAsync{T1, T2}(IDbConnection, string, object, CommandOptions{ValueTuple{T1, T2}}, CancellationToken)"/>
    public static ValueTask<List<(T1, T2)>> QueryAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with command options and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
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
    /// <returns>A task containing a list of tuples with mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the query within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Async query with transaction
    /// using var tx = connection.BeginTransaction();
    /// var results = await connection.QueryAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="QueryAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<List<(T1, T2)>> QueryAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and command options and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
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
    /// <returns>A task containing a list of tuples with mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full async example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var results = await connection.QueryAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<List<(T1, T2)>> QueryAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with multi-entity command options and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    public static ValueTask<List<(T1, T2)>> QueryAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and multi-entity command options and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    public static ValueTask<List<(T1, T2)>> QueryAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    #endregion

    #region Legacy Multi-Entity QueryAsync APIs (Marked as Obsolete for Consistency)

    /// <summary>
    /// Asynchronously executes a query and maps columns to two entity types by property name.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static ValueTask<List<(T1, T2)>> QueryAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        if (connection is not DbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return ExecuteReaderAsync<List<(T1, T2)>>(connection, sql, parameters, options, async (reader, ct) =>
        {
            // AUD-R26: these two obsolete overloads hardcoded 64, so a consumer who tuned
            // JauntyConfig.QueryResultCapacity for their workload silently got the default here and
            // nowhere else - every non-obsolete multi-entity path in QueryCore.cs already reads it.
            // The per-call ExpectedRowCount hint the non-obsolete paths also honour is not available:
            // these take the non-generic CommandOptions, which carries no such field, and widening a
            // public struct for two obsolete entry points is not worth it.
            var results = new List<(T1, T2)>(JauntyConfig.QueryResultCapacity);

            var dbReader = (DbDataReader)reader;

            if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                return results;

            var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);

                results.Add((t1, t2));
            }
            while (await dbReader.ReadAsync(ct).ConfigureAwait(false));

            return results;
        }, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a query, maps to two entity types, and combines them using a function.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static ValueTask<List<TResult>> QueryAsync<T1, T2, TResult>(
        this IDbConnection connection,
        string sql,
        Func<T1, T2, TResult> map,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ArgumentNullException.ThrowIfNull(map);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (map is null) throw new ArgumentNullException(nameof(map));
#endif

        if (connection is not DbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return ExecuteReaderAsync<List<TResult>>(connection, sql, parameters, options, async (reader, ct) =>
        {
            var results = new List<TResult>(JauntyConfig.QueryResultCapacity);

            var dbReader = (DbDataReader)reader;

            if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                return results;

            var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, dbReader);
                mapping.ApplyT2(t2, dbReader);

                results.Add(map(t1, t2));
            }
            while (await dbReader.ReadAsync(ct).ConfigureAwait(false));

            return results;
        }, cancellationToken);
    }

    #endregion

    #region Multi-Entity QueryFirstAsync APIs

    /// <summary>
    /// Asynchronously executes a SQL query and returns the first row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a tuple with the first mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// For a method that returns <see langword="null"/> instead of throwing, use 
    /// <see cref="QueryFirstOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order with its customer
    /// var (order, customer) = await connection.QueryFirstAsync&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<(T1, T2)> QueryFirstAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and returns the first row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a tuple with the first mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order for a specific customer
    /// var (order, customer) = await connection.QueryFirstAsync&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirstOrDefaultAsync{T1, T2}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<(T1, T2)> QueryFirstAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with command options and returns the first row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a tuple with the first mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order with transaction
    /// using var tx = connection.BeginTransaction();
    /// var (order, customer) = await connection.QueryFirstAsync&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryFirstAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<(T1, T2)> QueryFirstAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and command options and returns the first row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
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
    /// <returns>A task containing a tuple with the first mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full async example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var (order, customer) = await connection.QueryFirstAsync&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns no results, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<(T1, T2)> QueryFirstAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with multi-entity command options and returns the first row mapped to two entity types.
    /// </summary>
    public static ValueTask<(T1, T2)> QueryFirstAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and multi-entity command options and returns the first row mapped to two entity types.
    /// </summary>
    public static ValueTask<(T1, T2)> QueryFirstAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a query and returns the first row mapped to two entity types.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static ValueTask<(T1, T2)> QueryFirstAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        if (connection is not DbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return ExecuteReaderAsync<(T1, T2)>(connection, sql, parameters, options, async (reader, ct) =>
        {
            var dbReader = (DbDataReader)reader;

            if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.");

            var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, dbReader);
            mapping.ApplyT2(t2, dbReader);

            return (t1, t2);
        }, cancellationToken);
    }

    #endregion

    #region Multi-Entity QueryFirstOrDefaultAsync APIs

    /// <summary>
    /// Asynchronously executes a SQL query and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing a nullable tuple with the first mapped entities, or <see langword="null"/> if no results are found.
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
    /// // Get first order with customer or null
    /// var result = await connection.QueryFirstOrDefaultAsync&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 999 });
    /// 
    /// if (result != null)
    /// {
    ///     var (order, customer) = result.Value;
    ///     Console.WriteLine($"Order {order.OrderId} by {customer.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryFirstAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<(T1, T2)?> QueryFirstOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing a nullable tuple with the first mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order for a customer or null
    /// var result = await connection.QueryFirstOrDefaultAsync&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 999 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirstAsync{T1, T2}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<(T1, T2)?> QueryFirstOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with command options and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing a nullable tuple with the first mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order with transaction
    /// using var tx = connection.BeginTransaction();
    /// var result = await connection.QueryFirstOrDefaultAsync&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<(T1, T2)?> QueryFirstOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and command options and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
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
    /// A task containing a nullable tuple with the first mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full async example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var result = await connection.QueryFirstOrDefaultAsync&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 999 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<(T1, T2)?> QueryFirstOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with multi-entity command options and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    public static ValueTask<(T1, T2)?> QueryFirstOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and multi-entity command options and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    public static ValueTask<(T1, T2)?> QueryFirstOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a query and returns the first row mapped to two entity types, or default if empty.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static ValueTask<(T1, T2)?> QueryFirstOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        if (connection is not DbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return ExecuteReaderAsync<(T1, T2)?>(connection, sql, parameters, options, async (reader, ct) =>
        {
            var dbReader = (DbDataReader)reader;

            if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                return null;

            var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, dbReader);
            mapping.ApplyT2(t2, dbReader);

            return (t1, t2);
        }, cancellationToken);
    }

    #endregion

    #region Multi-Entity QuerySingleAsync APIs

    /// <summary>
    /// Asynchronously executes a SQL query and returns exactly one row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a tuple with the single mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>The query returns no results</description></item>
    /// <item><description>The query returns more than one result</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with its customer (expects exactly one match)
    /// var (order, customer) = await connection.QuerySingleAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 1 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns zero or more than one result, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QuerySingleOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirstAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<(T1, T2)> QuerySingleAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and returns exactly one row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a tuple with the single mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query doesn't return exactly one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with customer by order ID
    /// var (order, customer) = await connection.QuerySingleAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 1 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns zero or more than one result, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QuerySingleAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QuerySingleOrDefaultAsync{T1, T2}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<(T1, T2)> QuerySingleAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with command options and returns exactly one row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>A task containing a tuple with the single mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query doesn't return exactly one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with transaction
    /// using var tx = connection.BeginTransaction();
    /// var (order, customer) = await connection.QuerySingleAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns zero or more than one result, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QuerySingleAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<(T1, T2)> QuerySingleAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and command options and returns exactly one row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
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
    /// <returns>A task containing a tuple with the single mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query doesn't return exactly one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full async example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var (order, customer) = await connection.QuerySingleAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 1 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns zero or more than one result, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QuerySingleAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<(T1, T2)> QuerySingleAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with multi-entity command options and returns exactly one row mapped to two entity types.
    /// </summary>
    public static ValueTask<(T1, T2)> QuerySingleAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and multi-entity command options and returns exactly one row mapped to two entity types.
    /// </summary>
    public static ValueTask<(T1, T2)> QuerySingleAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a query and returns exactly one row mapped to two entity types.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static ValueTask<(T1, T2)> QuerySingleAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        if (connection is not DbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return ExecuteReaderAsync<(T1, T2)>(connection, sql, parameters, options, async (reader, ct) =>
        {
            var dbReader = (DbDataReader)reader;

            if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.");

            var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, dbReader);
            mapping.ApplyT2(t2, dbReader);

            return await dbReader.ReadAsync(ct).ConfigureAwait(false)
                ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.")
                : ((T1, T2))(t1, t2);
        }, cancellationToken);
    }

    #endregion

    #region Multi-Entity QuerySingleOrDefaultAsync APIs

    /// <summary>
    /// Asynchronously executes a SQL query and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing a nullable tuple with the single mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with customer or null (expects 0 or 1 match)
    /// var result = await connection.QuerySingleOrDefaultAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 999 });
    /// 
    /// if (result != null)
    /// {
    ///     var (order, customer) = result.Value;
    ///     Console.WriteLine($"Order {order.OrderId} by {customer.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns more than one result, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QuerySingleAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QueryFirstOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    public static ValueTask<(T1, T2)?> QuerySingleOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing a nullable tuple with the single mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with customer or null
    /// var result = await connection.QuerySingleOrDefaultAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 999 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns more than one result, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QuerySingleOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="QuerySingleAsync{T1, T2}(IDbConnection, string, object, CancellationToken)"/>
    public static ValueTask<(T1, T2)?> QuerySingleOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with command options and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// A task containing a nullable tuple with the single mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with transaction
    /// using var tx = connection.BeginTransaction();
    /// var result = await connection.QuerySingleOrDefaultAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns more than one result, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QuerySingleOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<(T1, T2)?> QuerySingleOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and command options and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
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
    /// A task containing a nullable tuple with the single mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full async example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var result = await connection.QuerySingleOrDefaultAsync&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 999 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when the query returns more than one result, 
    /// when a property has no matching column, or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QuerySingleOrDefaultAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static ValueTask<(T1, T2)?> QuerySingleOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        CommandOptions<(T1, T2)> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with multi-entity command options and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    public static ValueTask<(T1, T2)?> QuerySingleOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and multi-entity command options and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    public static ValueTask<(T1, T2)?> QuerySingleOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object parameters,
        MultiEntityCommandOptions<T1, T2> options,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a query and returns exactly one row mapped to two entity types, or default if empty.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static ValueTask<(T1, T2)?> QuerySingleOrDefaultAsync<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default,
        CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
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
        if (connection is not DbConnection)
            throw new InvalidOperationException("Async connection requires a DbConnection or its subclass");

        return ExecuteReaderAsync<(T1, T2)?>(connection, sql, parameters, options, async (reader, ct) =>
        {
            var dbReader = (DbDataReader)reader;

            if (!await dbReader.ReadAsync(ct).ConfigureAwait(false))
                return null;

            var mapping = MultiEntityMapper<T1, T2>.Build(dbReader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, dbReader);
            mapping.ApplyT2(t2, dbReader);

            return await dbReader.ReadAsync(ct).ConfigureAwait(false)
                ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.")
                : ((T1, T2)?)(t1, t2);
        }, cancellationToken);
    }

    #endregion

    #region Multi-Entity QueryStreamAsync APIs

#if ASYNC_ENUMERABLE_SUPPORT
    /// <summary>
    /// Asynchronously executes a SQL query and streams rows mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">
    /// The SQL query to execute. Should return columns that can be mapped to both <typeparamref name="T1"/>
    /// and <typeparamref name="T2"/>. Use SQL aliases to disambiguate columns with the same name.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>An async enumerable of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> The connection stays open until enumeration completes.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column,
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryStreamAsync{T1, T2}(IDbConnection, string, object, CancellationToken)"/>
    /// <seealso cref="QueryAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    public static IAsyncEnumerable<(T1, T2)> QueryStreamAsync<T1, T2>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and streams rows mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>An async enumerable of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <seealso cref="QueryStreamAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    public static IAsyncEnumerable<(T1, T2)> QueryStreamAsync<T1, T2>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with command options and streams rows mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the asynchronous operation. Defaults to <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>An async enumerable of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <seealso cref="QueryStreamAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static IAsyncEnumerable<(T1, T2)> QueryStreamAsync<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and command options and streams rows mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
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
    /// <returns>An async enumerable of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> The connection stays open until enumeration completes.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the connection is not a <see cref="DbConnection"/>, when a property has no matching column,
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryStreamAsync{T1, T2}(IDbConnection, string, CancellationToken)"/>
    public static IAsyncEnumerable<(T1, T2)> QueryStreamAsync<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with multi-entity command options and streams rows mapped to two entity types.
    /// </summary>
    public static IAsyncEnumerable<(T1, T2)> QueryStreamAsync<T1, T2>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }

    /// <summary>
    /// Asynchronously executes a SQL query with parameters and multi-entity command options and streams rows mapped to two entity types.
    /// </summary>
    public static IAsyncEnumerable<(T1, T2)> QueryStreamAsync<T1, T2>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
#endif

    #endregion
}