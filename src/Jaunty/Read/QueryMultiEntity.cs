using System.Data;

using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Read;

namespace Jaunty;

public static partial class Jaunty
{
    #region Consistent Multi-Entity Query APIs (Following same pattern as regular Query APIs)

    /// <summary>
    /// Executes a SQL query and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">
    /// The SQL query to execute. Should return columns that can be mapped to both <typeparamref name="T1"/> 
    /// and <typeparamref name="T2"/>. Use SQL aliases to disambiguate columns with the same name 
    /// (e.g., <c>o.id AS OrderId, c.id AS CustomerId</c>).
    /// </param>
    /// <returns>A list of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// This method uses <strong>strict mapping mode</strong>. All public writable properties on both 
    /// <typeparamref name="T1"/> and <typeparamref name="T2"/> must have matching columns in the result set.
    /// </para>
    /// <para>
    /// Columns are matched to entity properties using case-insensitive name matching. If a column name 
    /// matches properties in both types, <typeparamref name="T1"/> has priority.
    /// </para>
    /// <para>
    /// <strong>Tip:</strong> Use SQL aliases to ensure unique column names when joining tables:
    /// <code>SELECT o.id AS OrderId, o.Date, c.id AS CustomerId, c.Name FROM Orders o JOIN Customers c ON o.CustomerId = c.Id</code>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class Order
    /// {
    ///     public int OrderId { get; set; }
    ///     public DateTime Date { get; set; }
    ///     public decimal Total { get; set; }
    /// }
    /// 
    /// public class Customer
    /// {
    ///     public int CustomerId { get; set; }
    ///     public string Name { get; set; }
    ///     public string Email { get; set; }
    /// }
    /// 
    /// // Query orders with their customers
    /// var results = connection.Query&lt;Order, Customer&gt;(
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
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string, object)"/>
    /// <seealso cref="QueryFirst{T1, T2}(IDbConnection, string)"/>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
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
        return QueryMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>A list of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Query orders with their customers for a specific customer
    /// var results = connection.Query&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string, object, CommandOptions{ValueTuple{T1, T2}})"/>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with command options and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution. Use 
    /// <see cref="CommandOptions{T}.WithTransaction(IDbTransaction)"/> for transactions or
    /// <see cref="CommandOptions{T}.WithTimeout(int)"/> for command timeout.
    /// </param>
    /// <returns>A list of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// Use this overload when you need to execute the query within a transaction or with a specific timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Query with transaction
    /// using var tx = connection.BeginTransaction();
    /// var results = connection.Query&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="CommandOptions{T}"/>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string)"/>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
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
        return QueryMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>A list of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload, combining parameter binding with execution options.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var results = connection.Query&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with multi-entity command options and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
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
        return QueryMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and multi-entity command options and maps columns to two entity types by property name, returning a list of tuples.
    /// </summary>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    #endregion

    #region Legacy Multi-Entity Query APIs (Marked as Obsolete for Consistency)

    /// <summary>
    /// Executes a query and maps columns to two entity types by property name.
    /// Columns are matched to entity properties using case-insensitive name matching.
    /// T1 has priority - if a column matches both types, it maps to T1.
    /// Use SQL aliases to disambiguate (e.g., "o.id AS OrderId, c.id AS CustomerId").
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, object? parameters = null, CommandOptions options = default) where T1 : new() where T2 : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            // AUD-R26: these two obsolete overloads hardcoded 64, so a consumer who tuned
            // JauntyConfig.QueryResultCapacity for their workload silently got the default here and
            // nowhere else - every non-obsolete multi-entity path in QueryCore.cs already reads it.
            // The per-call ExpectedRowCount hint the non-obsolete paths also honour is not available:
            // these take the non-generic CommandOptions, which carries no such field, and widening a
            // public struct for the obsolete entry points is not worth it - there are thirteen of them
            // across this file and QueryMultiEntityAsync.cs, not the two this comment first claimed.
            var results = new List<(T1, T2)>(JauntyConfig.QueryResultCapacity);

            if (!reader.Read())
                return results;

            // Build mapping on first row
            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.Map(t1, t2, reader);

                results.Add((t1, t2));
            }
            while (reader.Read());

            return results;
        });
    }

    /// <summary>
    /// Executes a query, maps to two entity types, and combines them using a function.
    /// </summary>
    /// <remarks>
    /// AUD-R34-003, residual and deliberately not fixed. Passing options <i>positionally</i> -
    /// <c>Query&lt;T1, T2, R&gt;(sql, map, CommandOptions.WithTimeout(60))</c> - binds them to
    /// <paramref name="parameters"/> and discards them, and unlike the rest of the multi-entity
    /// surface there is no <c>CommandOptions&lt;T&gt;</c> sibling for the AUD-R34-002 conversion to
    /// select. The obvious remedy, a <c>(sql, map, CommandOptions)</c> overload, is a <b>source
    /// break</b>: both overloads would then be applicable to the named form
    /// <c>(sql, map, options: x)</c>, which is CS0121 - the library's own tests call it that way.
    /// So: use the named argument, which is correct today, or move to the non-obsolete overload
    /// taking <c>CommandOptions&lt;(T1, T2)&gt;</c>, which this whole overload is obsolete in favour
    /// of anyway.
    /// </remarks>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static List<TResult> Query<T1, T2, TResult>(this IDbConnection connection, string sql, Func<T1, T2, TResult> map, object? parameters = null, CommandOptions options = default) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(map);
#else
        if (map is null) throw new ArgumentNullException(nameof(map));
#endif

        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            var results = new List<TResult>(JauntyConfig.QueryResultCapacity);

            if (!reader.Read())
                return results;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.Map(t1, t2, reader);

                results.Add(map(t1, t2));
            }
            while (reader.Read());

            return results;
        });
    }

    #endregion

    #region Multi-Entity QueryFirst APIs

    /// <summary>
    /// Executes a SQL query and returns the first row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>A tuple containing the first mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// For a method that returns <see langword="null"/> instead of throwing, use 
    /// <see cref="QueryFirstOrDefault{T1, T2}(IDbConnection, string)"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order with its customer
    /// var (order, customer) = connection.QueryFirst&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id");
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns no results, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefault{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string)"/>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
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
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns the first row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>A tuple containing the first mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order for a specific customer
    /// var (order, customer) = connection.QueryFirst&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns no results, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirst{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="QueryFirstOrDefault{T1, T2}(IDbConnection, string, object)"/>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns the first row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <returns>A tuple containing the first mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order with transaction
    /// using var tx = connection.BeginTransaction();
    /// var (order, customer) = connection.QueryFirst&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns no results, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryFirst{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
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
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and returns the first row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>A tuple containing the first mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if no results are returned.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var (order, customer) = connection.QueryFirst&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns no results, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirst{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with multi-entity command options and returns the first row mapped to two entity types.
    /// </summary>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
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
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and multi-entity command options and returns the first row mapped to two entity types.
    /// </summary>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query and returns the first row mapped to two entity types.
    /// Throws if no rows are returned.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, object? parameters = null, CommandOptions options = default) where T1 : new() where T2 : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.");

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.Map(t1, t2, reader);

            return (t1, t2);
        });
    }

    #endregion

    #region Multi-Entity QueryFirstOrDefault APIs

    /// <summary>
    /// Executes a SQL query and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>
    /// A nullable tuple containing the first mapped entities, or <see langword="null"/> if no results are found.
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
    /// var result = connection.QueryFirstOrDefault&lt;Order, Customer&gt;(
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
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryFirst{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string)"/>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>
    /// A nullable tuple containing the first mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get first order for a customer or null
    /// var result = connection.QueryFirstOrDefault&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 999 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefault{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="QueryFirst{T1, T2}(IDbConnection, string, object)"/>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <returns>
    /// A nullable tuple containing the first mapped entities, or <see langword="null"/> if no results are found.
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
    /// var result = connection.QueryFirstOrDefault&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefault{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>
    /// A nullable tuple containing the first mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Returns <see langword="null"/> for empty result sets instead of throwing.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var result = connection.QueryFirstOrDefault&lt;Order, Customer&gt;(
    ///     "SELECT TOP 1 o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 999 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryFirstOrDefault{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with multi-entity command options and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and multi-entity command options and returns the first row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query and returns the first row mapped to two entity types, or default if empty.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, object? parameters = null, CommandOptions options = default) where T1 : new() where T2 : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return ((T1, T2)?)null;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.Map(t1, t2, reader);

            return (t1, t2);
        });
    }

    #endregion

    #region Multi-Entity QuerySingle APIs

    /// <summary>
    /// Executes a SQL query and returns exactly one row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>A tuple containing the single mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
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
    /// var (order, customer) = connection.QuerySingle&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 1 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns zero or more than one result, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QuerySingleOrDefault{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="QueryFirst{T1, T2}(IDbConnection, string)"/>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
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
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns exactly one row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>A tuple containing the single mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query doesn't return exactly one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with customer by order ID
    /// var (order, customer) = connection.QuerySingle&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 1 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns zero or more than one result, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QuerySingle{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="QuerySingleOrDefault{T1, T2}(IDbConnection, string, object)"/>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns exactly one row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <returns>A tuple containing the single mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query doesn't return exactly one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with transaction
    /// using var tx = connection.BeginTransaction();
    /// var (order, customer) = connection.QuerySingle&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns zero or more than one result, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QuerySingle{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
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
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and returns exactly one row mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>A tuple containing the single mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query doesn't return exactly one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// var (order, customer) = connection.QuerySingle&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 1 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns zero or more than one result, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QuerySingle{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with multi-entity command options and returns exactly one row mapped to two entity types.
    /// </summary>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
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
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and multi-entity command options and returns exactly one row mapped to two entity types.
    /// </summary>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query and returns exactly one row mapped to two entity types.
    /// Throws if zero or more than one row is returned.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, object? parameters = null, CommandOptions options = default) where T1 : new() where T2 : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(T1).Name}, {typeof(T2).Name})'.");

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.Map(t1, t2, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : (t1, t2);
        });
    }

    #endregion

    #region Multi-Entity QuerySingleOrDefault APIs

    /// <summary>
    /// Executes a SQL query and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>
    /// A nullable tuple containing the single mapped entities, or <see langword="null"/> if no results are found.
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
    /// var result = connection.QuerySingleOrDefault&lt;Order, Customer&gt;(
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
    /// Thrown when the query returns more than one result, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QuerySingle{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="QueryFirstOrDefault{T1, T2}(IDbConnection, string)"/>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>
    /// A nullable tuple containing the single mapped entities, or <see langword="null"/> if no results are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Throws <see cref="InvalidOperationException"/> if the query returns more than one result.</strong>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Get exactly one order with customer or null
    /// var result = connection.QuerySingleOrDefault&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 999 });
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns more than one result, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QuerySingleOrDefault{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="QuerySingle{T1, T2}(IDbConnection, string, object)"/>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with command options and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <returns>
    /// A nullable tuple containing the single mapped entities, or <see langword="null"/> if no results are found.
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
    /// var result = connection.QuerySingleOrDefault&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns more than one result, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QuerySingleOrDefault{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>
    /// A nullable tuple containing the single mapped entities, or <see langword="null"/> if no results are found.
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
    /// var result = connection.QuerySingleOrDefault&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE o.id = @OrderId",
    ///     new { OrderId = 999 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx));
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query returns more than one result, when a property has no matching column, 
    /// or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QuerySingleOrDefault{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with multi-entity command options and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and multi-entity command options and returns exactly one row mapped to two entity types, or <see langword="null"/> if empty.
    /// </summary>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query and returns exactly one row mapped to two entity types, or default if empty.
    /// Throws if more than one row is returned.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, object? parameters = null, CommandOptions options = default) where T1 : new() where T2 : new()
    {
        return ExecuteReader(connection, sql, parameters, options, reader =>
        {
            if (!reader.Read())
                return ((T1, T2)?)null;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.Map(t1, t2, reader);

            return reader.Read() ? throw new InvalidOperationException($"Sequence contains more than one element of type '({typeof(T1).Name}, {typeof(T2).Name})'.") : ((T1, T2)?)(t1, t2);
        });
    }

    #endregion

    #region Multi-Entity QueryStream APIs

    /// <summary>
    /// Executes a SQL query and streams rows mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <returns>An enumerable of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// Uses <strong>strict mapping mode</strong> - all properties must have matching columns.
    /// </para>
    /// <para>
    /// <strong>Important:</strong> The connection stays open until enumeration completes. Use a <c>using</c> statement 
    /// or ensure you fully enumerate the results before closing the connection.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stream orders with their customers (connection stays open during enumeration)
    /// foreach (var (order, customer) in connection.QueryStream&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id"))
    /// {
    ///     Console.WriteLine($"Order {order.OrderId} by {customer.Name}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryStream{T1, T2}(IDbConnection, string, object)"/>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string)"/>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
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
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and streams rows mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <returns>An enumerable of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> The connection stays open until enumeration completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stream orders for a specific customer
    /// foreach (var (order, customer) in connection.QueryStream&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 }))
    /// {
    ///     Console.WriteLine($"Order {order.OrderId}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryStream{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="Query{T1, T2}(IDbConnection, string, object)"/>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with command options and streams rows mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="options">
    /// Command options for configuring the query execution.
    /// </param>
    /// <returns>An enumerable of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> The connection stays open until enumeration completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stream orders with transaction
    /// using var tx = connection.BeginTransaction();
    /// foreach (var (order, customer) in connection.QueryStream&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id",
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx)))
    /// {
    ///     Console.WriteLine($"Order {order.OrderId}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <seealso cref="QueryStream{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
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
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and streams rows mapped to two entity types.
    /// </summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="parameters">
    /// An anonymous object or dictionary containing parameter values.
    /// </param>
    /// <param name="options">
    /// Command options for transaction, timeout, or custom mapper configuration.
    /// </param>
    /// <returns>An enumerable of tuples containing mapped entities of type (<typeparamref name="T1"/>, <typeparamref name="T2"/>).</returns>
    /// <remarks>
    /// <para>
    /// <strong>Important:</strong> The connection stays open until enumeration completes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Full example with transaction and parameters
    /// using var tx = connection.BeginTransaction();
    /// foreach (var (order, customer) in connection.QueryStream&lt;Order, Customer&gt;(
    ///     "SELECT o.id AS OrderId, o.Date, o.Total, c.id AS CustomerId, c.Name, c.Email " +
    ///     "FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    ///     "WHERE c.id = @CustomerId",
    ///     new { CustomerId = 5 },
    ///     CommandOptions&lt;(Order, Customer)&gt;.WithTransaction(tx)))
    /// {
    ///     Console.WriteLine($"Order {order.OrderId}");
    /// }
    /// </code>
    /// </example>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a property has no matching column or when a non-nullable property receives a NULL value.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when parameter count doesn't match the SQL.
    /// </exception>
    /// <seealso cref="QueryStream{T1, T2}(IDbConnection, string)"/>
    /// <seealso cref="CommandOptions{T}"/>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with multi-entity command options and streams rows mapped to two entity types.
    /// </summary>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
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
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a SQL query with parameters and multi-entity command options and streams rows mapped to two entity types.
    /// </summary>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2> options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, parameters, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query and streams rows mapped to two entity types.
    /// Connection stays open until enumeration completes.
    /// </summary>
    [Obsolete("Use the overload with CommandOptions<(T1, T2)> instead")]
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, object? parameters = null, CommandOptions options = default) where T1 : new() where T2 : new()
    {
        return QueryStreamCore<T1, T2>(connection, sql, parameters, options);
    }

    // Eagerly validates (connection/sql) rather than deferring to first enumeration: a
    // yield-return method only executes its body once enumerated, so a check placed there would
    // silently never run for a caller who discards the returned IEnumerable<(T1, T2)> (or breaks
    // out of a foreach early) without enumerating it. Splitting into a thin eager wrapper plus a
    // private iterator ensures misuse (e.g. a null connection) is caught immediately at call time
    // instead of being deferred to whenever/if enumeration happens.
    private static IEnumerable<(T1, T2)> QueryStreamCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions options) where T1 : new() where T2 : new()
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
        return QueryStreamCoreIterator<T1, T2>(connection, sql, parameters, options);
    }

    private static IEnumerable<(T1, T2)> QueryStreamCoreIterator<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions options) where T1 : new() where T2 : new()
    {
        bool wasClosed = connection.State == ConnectionState.Closed;

        IDbCommand? command = null;
        IDataReader? reader = null;
        MultiEntityMapper<T1, T2>? mapping = null;

        try
        {
            if (wasClosed) connection.Open();

            command = connection.CreateCommand();
            command.CommandText = sql;

            // AUD-R25: this iterator applied neither CommandType nor the logger, unlike every other
            // obsolete multi-entity overload in this file (which route through ExecuteReader) and
            // unlike the non-obsolete replacement QueryStreamMultiEntityCore. Passing
            // CommandOptions.AsStoredProcedure() therefore left the command as CommandType.Text, so
            // the provider executed the procedure *name* as a raw SQL statement, and the command
            // never reached JauntyConfig.Logger. AUD-R12 amended this same iterator for the
            // silently-dropped non-DbTransaction without carrying these two assignments across.
            if (options.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
                command.CommandType = options.CommandType;

            // A DbConnection's IDbCommand.Transaction setter is DbCommand's explicit interface
            // implementation, which casts to DbTransaction internally - assigning a non-DbTransaction
            // IDbTransaction through it throws an opaque InvalidCastException. Validate via
            // AsyncTransactionValidator first (mirroring InsertCoreDirect) so an incompatible
            // transaction gets Jaunty's clear ArgumentException instead of being silently dropped.
            if (options.Transaction is not null)
            {
                command.Transaction = connection is System.Data.Common.DbConnection
                    ? AsyncTransactionValidator.RequireDbTransaction(options.Transaction)
                    : options.Transaction;
            }

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            JauntyConfig.Logger?.Invoke(command.CommandText, parameters);

            reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.Map(t1, t2, reader);

                yield return (t1, t2);
            }
            while (reader.Read());
        }
        finally
        {
            reader?.Dispose();
            command?.Dispose();
            if (wasClosed && connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }

    #endregion
}