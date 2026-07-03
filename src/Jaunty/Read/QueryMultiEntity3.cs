using System;
using System.Collections.Generic;
using System.Data;
using Jaunty.Core;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Parameters;
using Jaunty.Internals.Read;

namespace Jaunty;

/// <summary>Arity-3 multi-entity query API.</summary>
public static partial class Jaunty
{
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new() { return QueryMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new() { return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QueryMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QueryMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    public static List<(T1, T2, T3)> Query<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QueryMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    public static (T1, T2, T3) QueryFirst<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QueryFirstOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QueryFirstOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    public static (T1, T2, T3) QuerySingle<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    public static (T1, T2, T3)? QuerySingleOrDefault<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QuerySingleOrDefaultMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query and maps 3 entity types.</summary>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql) where T1 : new() where T2 : new() where T3 : new() { return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, null, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and maps 3 entity types.</summary>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new() where T3 : new() { return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, default, MappingMode.Strict); }
    /// <summary>Executes a SQL query with command options and maps 3 entity types.</summary>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and command options and maps 3 entity types.</summary>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2, T3)> options) where T1 : new() where T2 : new() where T3 : new() { return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with multi-entity command options and maps 3 entity types.</summary>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, null, options, MappingMode.Strict); }
    /// <summary>Executes a SQL query with parameters and multi-entity command options and maps 3 entity types.</summary>
    public static IEnumerable<(T1, T2, T3)> QueryStream<T1, T2, T3>(this IDbConnection connection, string sql, object parameters, MultiEntityCommandOptions<T1, T2, T3> options) where T1 : new() where T2 : new() where T3 : new() { return QueryStreamMultiEntityCore<T1, T2, T3>(connection, sql, parameters, options, MappingMode.Strict); }
}
