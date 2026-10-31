using System.Data;
using System.Data.Common;

using Extrode.Jaunty.Core;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Internals.Read;

namespace Extrode.Jaunty;

/// <summary>Arity-4 multi-entity async query API.</summary>
public static partial class Jaunty
{
    /// <summary>Asynchronously executes a SQL query and maps 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<List<(T1, T2, T3, T4)>> QueryAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<List<(T1, T2, T3, T4)>> QueryAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<List<(T1, T2, T3, T4)>> QueryAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<List<(T1, T2, T3, T4)>> QueryAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<List<(T1, T2, T3, T4)>> QueryAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<List<(T1, T2, T3, T4)>> QueryAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query and maps 4 entity types, returning the first row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set is empty.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QueryFirstAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 4 entity types, returning the first row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set is empty.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QueryFirstAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 4 entity types, returning the first row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set is empty.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QueryFirstAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 4 entity types, returning the first row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set is empty.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QueryFirstAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 4 entity types, returning the first row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set is empty.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QueryFirstAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 4 entity types, returning the first row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set is empty.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QueryFirstAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query and maps 4 entity types, returning the first row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 4 entity types, returning the first row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 4 entity types, returning the first row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 4 entity types, returning the first row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 4 entity types, returning the first row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 4 entity types, returning the first row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query and maps 4 entity types, returning a single row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set does not contain exactly one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QuerySingleAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 4 entity types, returning a single row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set does not contain exactly one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QuerySingleAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 4 entity types, returning a single row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set does not contain exactly one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QuerySingleAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 4 entity types, returning a single row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set does not contain exactly one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QuerySingleAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 4 entity types, returning a single row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set does not contain exactly one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QuerySingleAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 4 entity types, returning a single row.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set does not contain exactly one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)> QuerySingleAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query and maps 4 entity types, returning a single row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set contains more than one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 4 entity types, returning a single row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set contains more than one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 4 entity types, returning a single row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set contains more than one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 4 entity types, returning a single row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set contains more than one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 4 entity types, returning a single row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set contains more than one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 4 entity types, returning a single row or null.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>, or when the result set contains more than one row.</exception>
    public static ValueTask<(T1, T2, T3, T4)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
#if ASYNC_ENUMERABLE_SUPPORT
    /// <summary>Asynchronously executes a SQL query and streams 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of tuples, one per row, read as it is enumerated.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static IAsyncEnumerable<(T1, T2, T3, T4)> QueryStreamAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and streams 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of tuples, one per row, read as it is enumerated.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static IAsyncEnumerable<(T1, T2, T3, T4)> QueryStreamAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and streams 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of tuples, one per row, read as it is enumerated.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static IAsyncEnumerable<(T1, T2, T3, T4)> QueryStreamAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and streams 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of tuples, one per row, read as it is enumerated.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static IAsyncEnumerable<(T1, T2, T3, T4)> QueryStreamAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and streams 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of tuples, one per row, read as it is enumerated.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static IAsyncEnumerable<(T1, T2, T3, T4)> QueryStreamAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and streams 4 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T4">The fourth entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection. Must be a <see cref="DbConnection"/>.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An asynchronous stream of tuples, one per row, read as it is enumerated.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="connection"/> is not a <see cref="DbConnection"/>.</exception>
    public static IAsyncEnumerable<(T1, T2, T3, T4)> QueryStreamAsync<T1, T2, T3, T4>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new()
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
        return connection is not DbConnection dbConnection
            ? throw new InvalidOperationException("Async connection requires a DbConnection or its subclass")
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
#endif
}
