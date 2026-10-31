using System;
using System.Collections.Generic;
using System.Data;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Internals.Read;

namespace Extrode.Jaunty;

/// <summary>Arity-3 multi-entity query API.</summary>
public static partial class Jaunty
{
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>A list of tuples, one per row.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set is empty.</exception>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set is empty.</exception>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set is empty.</exception>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set is empty.</exception>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set is empty.</exception>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>The first row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set is empty.</exception>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>The first row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set does not contain exactly one row.</exception>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set does not contain exactly one row.</exception>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set does not contain exactly one row.</exception>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set does not contain exactly one row.</exception>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set does not contain exactly one row.</exception>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>The only row as a tuple.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set does not contain exactly one row.</exception>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set contains more than one row.</exception>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set contains more than one row.</exception>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set contains more than one row.</exception>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set contains more than one row.</exception>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set contains more than one row.</exception>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>The only row as a tuple, or <see langword="null"/> when the result set is empty.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the result set contains more than one row.</exception>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <returns>A lazily read sequence of tuples, one per row. The connection stays open until enumeration ends.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <returns>A lazily read sequence of tuples, one per row. The connection stays open until enumeration ends.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>A lazily read sequence of tuples, one per row. The connection stays open until enumeration ends.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Command options for transaction, timeout, command type or a custom tuple mapper.</param>
    /// <returns>A lazily read sequence of tuples, one per row. The connection stays open until enumeration ends.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>A lazily read sequence of tuples, one per row. The connection stays open until enumeration ends.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/> or <paramref name="sql"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    /// <typeparam name="T1">The first entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T2">The second entity type. Must have a parameterless constructor.</typeparam>
    /// <typeparam name="T3">The third entity type. Must have a parameterless constructor.</typeparam>
    /// <param name="connection">The database connection to execute the query against.</param>
    /// <param name="sql">The SQL query to execute. Alias columns that share a name across entities so each maps to the right one.</param>
    /// <param name="parameters">An object whose properties supply the values of the query parameters.</param>
    /// <param name="options">Per-call options: transaction, timeout, command type and expected row count.</param>
    /// <returns>A lazily read sequence of tuples, one per row. The connection stays open until enumeration ends.</returns>
    /// <remarks>Multi-entity reads map each entity in projection mode: a property with no matching column is left at its default value rather than reported.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connection"/>, <paramref name="sql"/> or <paramref name="parameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is empty or whitespace.</exception>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
}
