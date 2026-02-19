using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    #region Consistent Multi-Entity Query APIs (Following same pattern as regular Query APIs)

    /// <summary>
    /// Executes a query and maps columns to two entity types by property name.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Columns are matched to entity properties using case-insensitive name matching.
    /// T1 has priority - if a column matches both types, it maps to T1.
    /// Use SQL aliases to disambiguate (e.g., "o.id AS OrderId, c.id AS CustomerId").
    /// </summary>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
    {
        return QueryMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and maps columns to two entity types by property name.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Columns are matched to entity properties using case-insensitive name matching.
    /// T1 has priority - if a column matches both types, it maps to T1.
    /// Use SQL aliases to disambiguate (e.g., "o.id AS OrderId, c.id AS CustomerId").
    /// </summary>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
        return QueryMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with command options and maps columns to two entity types by property name.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Columns are matched to entity properties using case-insensitive name matching.
    /// T1 has priority - if a column matches both types, it maps to T1.
    /// Use SQL aliases to disambiguate (e.g., "o.id AS OrderId, c.id AS CustomerId").
    /// </summary>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
        return QueryMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and command options and maps columns to two entity types by property name.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Columns are matched to entity properties using case-insensitive name matching.
    /// T1 has priority - if a column matches both types, it maps to T1.
    /// Use SQL aliases to disambiguate (e.g., "o.id AS OrderId, c.id AS CustomerId").
    /// </summary>
    public static List<(T1, T2)> Query<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
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
            var results = new List<(T1, T2)>();

            if (!reader.Read())
                return results;

            // Build mapping on first row
            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                results.Add((t1, t2));
            }
            while (reader.Read());

            return results;
        });
    }

    /// <summary>
    /// Executes a query, maps to two entity types, and combines them using a function.
    /// </summary>
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
            var results = new List<TResult>();

            if (!reader.Read())
                return results;

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

                results.Add(map(t1, t2));
            }
            while (reader.Read());

            return results;
        });
    }

    #endregion

    #region Multi-Entity QueryFirst APIs

    /// <summary>
    /// Executes a query and returns the first row mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if no rows are returned.
    /// </summary>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
    {
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and returns the first row mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if no rows are returned.
    /// </summary>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with command options and returns the first row mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if no rows are returned.
    /// </summary>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
        return QueryFirstMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and command options and returns the first row mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if no rows are returned.
    /// </summary>
    public static (T1, T2) QueryFirst<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
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
                throw new InvalidOperationException("Sequence contains no elements");

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return (t1, t2);
        });
    }

    #endregion

    #region Multi-Entity QueryFirstOrDefault APIs

    /// <summary>
    /// Executes a query and returns the first row mapped to two entity types, or default if empty.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// </summary>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
    {
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and returns the first row mapped to two entity types, or default if empty.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// </summary>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with command options and returns the first row mapped to two entity types, or default if empty.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// </summary>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
        return QueryFirstOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and command options and returns the first row mapped to two entity types, or default if empty.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// </summary>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
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

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return (t1, t2);
        });
    }

    #endregion

    #region Multi-Entity QuerySingle APIs

    /// <summary>
    /// Executes a query and returns exactly one row mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if zero or more than one row is returned.
    /// </summary>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
    {
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and returns exactly one row mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if zero or more than one row is returned.
    /// </summary>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with command options and returns exactly one row mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if zero or more than one row is returned.
    /// </summary>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
        return QuerySingleMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and command options and returns exactly one row mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if zero or more than one row is returned.
    /// </summary>
    public static (T1, T2) QuerySingle<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
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
                throw new InvalidOperationException("Sequence contains no elements");

            var mapping = MultiEntityMapper<T1, T2>.Build(reader);

            var t1 = new T1();
            var t2 = new T2();

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return reader.Read() ? throw new InvalidOperationException("Sequence contains more than one element") : (t1, t2);
        });
    }

    #endregion

    #region Multi-Entity QuerySingleOrDefault APIs

    /// <summary>
    /// Executes a query and returns exactly one row mapped to two entity types, or default if empty.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if more than one row is returned.
    /// </summary>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
    {
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and returns exactly one row mapped to two entity types, or default if empty.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if more than one row is returned.
    /// </summary>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with command options and returns exactly one row mapped to two entity types, or default if empty.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if more than one row is returned.
    /// </summary>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
        return QuerySingleOrDefaultMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and command options and returns exactly one row mapped to two entity types, or default if empty.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Throws if more than one row is returned.
    /// </summary>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
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

            mapping.ApplyT1(t1, reader);
            mapping.ApplyT2(t2, reader);

            return reader.Read() ? throw new InvalidOperationException("Sequence contains more than one element") : ((T1, T2)?)(t1, t2);
        });
    }

    #endregion

    #region Multi-Entity QueryStream APIs

    /// <summary>
    /// Executes a query and streams rows mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Connection stays open until enumeration completes.
    /// </summary>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql) where T1 : new() where T2 : new()
    {
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, null, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and streams rows mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Connection stays open until enumeration completes.
    /// </summary>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, object parameters) where T1 : new() where T2 : new()
    {
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, parameters, default, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with command options and streams rows mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Connection stays open until enumeration completes.
    /// </summary>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
        return QueryStreamMultiEntityCore<T1, T2>(connection, sql, null, options, MappingMode.Strict);
    }

    /// <summary>
    /// Executes a query with parameters and command options and streams rows mapped to two entity types.
    /// Uses strict mapping mode where all entity properties must have matching columns in the result set.
    /// Connection stays open until enumeration completes.
    /// </summary>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(this IDbConnection connection, string sql, object parameters, CommandOptions<(T1, T2)> options) where T1 : new() where T2 : new()
    {
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

    private static IEnumerable<(T1, T2)> QueryStreamCore<T1, T2>(IDbConnection connection, string sql, object? parameters, CommandOptions options) where T1 : new() where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif
        var wasClosed = connection.State == ConnectionState.Closed;

        IDbCommand? command = null;
        IDataReader? reader = null;
        MultiEntityMapper<T1, T2>? mapping = null;

        try
        {
            if (wasClosed) connection.Open();

            command = connection.CreateCommand();
            command.CommandText = sql;

            if (options.Transaction is System.Data.Common.DbTransaction dbTransaction)
                command.Transaction = dbTransaction;

            if (options.CommandTimeout.HasValue)
                command.CommandTimeout = options.CommandTimeout.Value;

            if (parameters is not null)
                ParameterBinder.Bind(command, parameters);

            reader = command.ExecuteReader();

            if (!reader.Read())
                yield break;

            mapping = MultiEntityMapper<T1, T2>.Build(reader);

            do
            {
                var t1 = new T1();
                var t2 = new T2();

                mapping.ApplyT1(t1, reader);
                mapping.ApplyT2(t2, reader);

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
