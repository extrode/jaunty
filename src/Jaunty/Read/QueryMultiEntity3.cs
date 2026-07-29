using System;
using System.Collections.Generic;
using System.Data;
using Jaunty.Core;
using Jaunty.Configuration;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Read;

namespace Jaunty;

/// <summary>Arity-3 multi-entity query API.</summary>
public static partial class Jaunty
{
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
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
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
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
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
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
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
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
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
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
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
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
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
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
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
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
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
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
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
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
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
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
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
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
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
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
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
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
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
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
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
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
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
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
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
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
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new()
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
        return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict);
    }
}
