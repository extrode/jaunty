using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Internals.Read;

namespace Jaunty;

/// <summary>Arity-5 multi-entity async query API.</summary>
public static partial class Jaunty
{
    /// <summary>Asynchronously executes a SQL query and maps 5 entity types.</summary>
    public static ValueTask<List<(T1, T2, T3, T4, T5)>> QueryAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 5 entity types.</summary>
    public static ValueTask<List<(T1, T2, T3, T4, T5)>> QueryAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 5 entity types.</summary>
    public static ValueTask<List<(T1, T2, T3, T4, T5)>> QueryAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 5 entity types.</summary>
    public static ValueTask<List<(T1, T2, T3, T4, T5)>> QueryAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 5 entity types.</summary>
    public static ValueTask<List<(T1, T2, T3, T4, T5)>> QueryAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 5 entity types.</summary>
    public static ValueTask<List<(T1, T2, T3, T4, T5)>> QueryAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query and maps 5 entity types, returning the first row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QueryFirstAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 5 entity types, returning the first row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QueryFirstAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 5 entity types, returning the first row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QueryFirstAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 5 entity types, returning the first row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QueryFirstAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 5 entity types, returning the first row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QueryFirstAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 5 entity types, returning the first row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QueryFirstAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query and maps 5 entity types, returning the first row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 5 entity types, returning the first row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 5 entity types, returning the first row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 5 entity types, returning the first row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 5 entity types, returning the first row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 5 entity types, returning the first row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QueryFirstOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryFirstOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query and maps 5 entity types, returning a single row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QuerySingleAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 5 entity types, returning a single row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QuerySingleAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 5 entity types, returning a single row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QuerySingleAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 5 entity types, returning a single row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QuerySingleAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 5 entity types, returning a single row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QuerySingleAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 5 entity types, returning a single row.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)> QuerySingleAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query and maps 5 entity types, returning a single row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and maps 5 entity types, returning a single row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and maps 5 entity types, returning a single row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and maps 5 entity types, returning a single row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and maps 5 entity types, returning a single row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and maps 5 entity types, returning a single row or null.</summary>
    public static ValueTask<(T1, T2, T3, T4, T5)?> QuerySingleOrDefaultAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QuerySingleOrDefaultMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
#if ASYNC_ENUMERABLE_SUPPORT
    /// <summary>Asynchronously executes a SQL query and streams 5 entity types.</summary>
    public static IAsyncEnumerable<(T1, T2, T3, T4, T5)> QueryStreamAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and streams 5 entity types.</summary>
    public static IAsyncEnumerable<(T1, T2, T3, T4, T5)> QueryStreamAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, default, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with command options and streams 5 entity types.</summary>
    public static IAsyncEnumerable<(T1, T2, T3, T4, T5)> QueryStreamAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and command options and streams 5 entity types.</summary>
    public static IAsyncEnumerable<(T1, T2, T3, T4, T5)> QueryStreamAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3, T4, T5)> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with multi-entity command options and streams 5 entity types.</summary>
    public static IAsyncEnumerable<(T1, T2, T3, T4, T5)> QueryStreamAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, null, options, MappingMode.Strict, cancellationToken);
    }
    /// <summary>Asynchronously executes a SQL query with parameters and multi-entity command options and streams 5 entity types.</summary>
    public static IAsyncEnumerable<(T1, T2, T3, T4, T5)> QueryStreamAsync<T1, T2, T3, T4, T5>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3, T4, T5> options, CancellationToken cancellationToken = default) where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
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
            : QueryStreamMultiEntityCoreAsync<T1, T2, T3, T4, T5>(dbConnection, sql, parameters, options, MappingMode.Strict, cancellationToken);
    }
#endif
}
