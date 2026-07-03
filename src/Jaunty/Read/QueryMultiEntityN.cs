using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL query and maps columns to three entity types by property name, returning a list of tuples.
    /// T1 has highest ordinal-claiming priority, then T2, then T3.
    /// Use SQL column aliases to disambiguate overlapping property names across types.
    /// </summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql)
        where T1 : new() where T2 : new() where T3 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and maps columns to three entity types by property name.
    /// </summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters)
        where T1 : new() where T2 : new() where T3 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with command options and maps columns to three entity types by property name.
    /// </summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions options)
        where T1 : new() where T2 : new() where T3 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Projection);
    }

    /// <summary>
    /// Executes a SQL query with parameters and command options and maps columns to three entity types by property name.
    /// </summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions options)
        where T1 : new() where T2 : new() where T3 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentNullException(nameof(sql));
#endif
        return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Projection);
    }
}
