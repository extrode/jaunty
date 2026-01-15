using System.Data;

using Jaunty.Core;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a query and maps columns to two entity types by property name.
    /// Columns are matched to entity properties using case-insensitive name matching.
    /// T1 has priority - if a column matches both types, it maps to T1.
    /// Use SQL aliases to disambiguate (e.g., "o.id AS OrderId, c.id AS CustomerId").
    /// </summary>
    public static List<(T1, T2)> Query<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
        where T1 : new()
        where T2 : new()
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
    public static List<TResult> Query<T1, T2, TResult>(
        this IDbConnection connection,
        string sql,
        Func<T1, T2, TResult> map,
        object? parameters = null,
        CommandOptions options = default)
        where T1 : new()
        where T2 : new()
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

    /// <summary>
    /// Executes a query and returns the first row mapped to two entity types.
    /// Throws if no rows are returned.
    /// </summary>
    public static (T1, T2) QueryFirst<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
        where T1 : new()
        where T2 : new()
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

    /// <summary>
    /// Executes a query and returns the first row mapped to two entity types, or default if empty.
    /// </summary>
    public static (T1, T2)? QueryFirstOrDefault<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
        where T1 : new()
        where T2 : new()
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

    /// <summary>
    /// Executes a query and returns exactly one row mapped to two entity types.
    /// Throws if zero or more than one row is returned.
    /// </summary>
    public static (T1, T2) QuerySingle<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
        where T1 : new()
        where T2 : new()
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

            if (reader.Read())
                throw new InvalidOperationException("Sequence contains more than one element");

            return (t1, t2);
        });
    }

    /// <summary>
    /// Executes a query and returns exactly one row mapped to two entity types, or default if empty.
    /// Throws if more than one row is returned.
    /// </summary>
    public static (T1, T2)? QuerySingleOrDefault<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
        where T1 : new()
        where T2 : new()
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

            if (reader.Read())
                throw new InvalidOperationException("Sequence contains more than one element");

            return (t1, t2);
        });
    }

    /// <summary>
    /// Executes a query and streams rows mapped to two entity types.
    /// Connection stays open until enumeration completes.
    /// </summary>
    public static IEnumerable<(T1, T2)> QueryStream<T1, T2>(
        this IDbConnection connection,
        string sql,
        object? parameters = null,
        CommandOptions options = default)
        where T1 : new()
        where T2 : new()
    {
        return QueryStreamCore<T1, T2>(connection, sql, parameters, options);
    }

    private static IEnumerable<(T1, T2)> QueryStreamCore<T1, T2>(
        IDbConnection connection,
        string sql,
        object? parameters,
        CommandOptions options)
        where T1 : new()
        where T2 : new()
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException(nameof(sql));
#endif
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;

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
            if (wasClosed && connection.State != System.Data.ConnectionState.Closed)
                connection.Close();
        }
    }
}

/// <summary>
/// Builds and caches column-to-property mappings for two entity types.
/// Uses property-name matching: each column is mapped to the first type that has a matching property.
/// </summary>
internal sealed class MultiEntityMapper<T1, T2>
    where T1 : new()
    where T2 : new()
{
    private readonly (int Ordinal, Action<T1, IDataRecord, int> Setter)[] _t1Setters;
    private readonly (int Ordinal, Action<T2, IDataRecord, int> Setter)[] _t2Setters;
    private readonly (int Ordinal, bool IsNonNullable, string PropertyName)[] _t1NullChecks;
    private readonly (int Ordinal, bool IsNonNullable, string PropertyName)[] _t2NullChecks;

    private MultiEntityMapper(
        (int, Action<T1, IDataRecord, int>)[] t1Setters,
        (int, Action<T2, IDataRecord, int>)[] t2Setters,
        (int, bool, string)[] t1NullChecks,
        (int, bool, string)[] t2NullChecks)
    {
        _t1Setters = t1Setters;
        _t2Setters = t2Setters;
        _t1NullChecks = t1NullChecks;
        _t2NullChecks = t2NullChecks;
    }

    public static MultiEntityMapper<T1, T2> Build(IDataReader reader)
    {
        // Get property lookups for each type
        var t1Meta = MetadataCache<T1>.Metadata;
        var t2Meta = MetadataCache<T2>.Metadata;

        var t1Props = t1Meta.Columns.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);
        var t2Props = t2Meta.Columns.ToDictionary(c => c.ColumnName, StringComparer.OrdinalIgnoreCase);

        // Also map by property name (for when column name differs)
        foreach (var col in t1Meta.Columns)
        {
            if (!t1Props.ContainsKey(col.Property.Name))
                t1Props[col.Property.Name] = col;
        }
        foreach (var col in t2Meta.Columns)
        {
            if (!t2Props.ContainsKey(col.Property.Name))
                t2Props[col.Property.Name] = col;
        }

        var t1Setters = new List<(int, Action<T1, IDataRecord, int>)>();
        var t2Setters = new List<(int, Action<T2, IDataRecord, int>)>();
        var t1NullChecks = new List<(int, bool, string)>();
        var t2NullChecks = new List<(int, bool, string)>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);

            // T1 has priority
            if (t1Props.TryGetValue(columnName, out var t1Col))
            {
                var setter = CreateSetter<T1>(t1Col.Property);
                t1Setters.Add((i, setter));
                t1NullChecks.Add((i, IsNonNullable(t1Col.Property.PropertyType), t1Col.Property.Name));
                continue; // T1 wins, don't check T2
            }

            // Then T2
            if (t2Props.TryGetValue(columnName, out var t2Col))
            {
                var setter = CreateSetter<T2>(t2Col.Property);
                t2Setters.Add((i, setter));
                t2NullChecks.Add((i, IsNonNullable(t2Col.Property.PropertyType), t2Col.Property.Name));
            }

            // If neither matches, column is ignored (partial mapping behavior)
        }

        return new MultiEntityMapper<T1, T2>(
            [.. t1Setters],
            [.. t2Setters],
            [.. t1NullChecks],
            [.. t2NullChecks]);
    }

    public void ApplyT1(T1 target, IDataRecord record)
    {
        for (int i = 0; i < _t1Setters.Length; i++)
        {
            var (ordinal, setter) = _t1Setters[i];
            var (_, isNonNullable, propName) = _t1NullChecks[i];

            if (!record.IsDBNull(ordinal))
                setter(target, record, ordinal);
            else if (isNonNullable)
                throw new InvalidOperationException(
                    $"Cannot assign NULL to non-nullable property '{propName}' on type '{typeof(T1).Name}'.");
        }
    }

    public void ApplyT2(T2 target, IDataRecord record)
    {
        for (int i = 0; i < _t2Setters.Length; i++)
        {
            var (ordinal, setter) = _t2Setters[i];
            var (_, isNonNullable, propName) = _t2NullChecks[i];

            if (!record.IsDBNull(ordinal))
                setter(target, record, ordinal);
            else if (isNonNullable)
                throw new InvalidOperationException(
                    $"Cannot assign NULL to non-nullable property '{propName}' on type '{typeof(T2).Name}'.");
        }
    }

    private static Action<TEntity, IDataRecord, int> CreateSetter<TEntity>(System.Reflection.PropertyInfo property)
    {
        var target = System.Linq.Expressions.Expression.Parameter(typeof(TEntity), "target");
        var record = System.Linq.Expressions.Expression.Parameter(typeof(IDataRecord), "record");
        var index = System.Linq.Expressions.Expression.Parameter(typeof(int), "index");

        var getValue = System.Linq.Expressions.Expression.Call(
            record,
            typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!,
            index);

        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType);

        System.Linq.Expressions.Expression valueExpression;

        if (underlyingType is not null)
        {
            var changeType = System.Linq.Expressions.Expression.Call(
                typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!,
                getValue,
                System.Linq.Expressions.Expression.Constant(underlyingType, typeof(Type)));
            valueExpression = System.Linq.Expressions.Expression.Convert(changeType, propertyType);
        }
        else
        {
            var changeType = System.Linq.Expressions.Expression.Call(
                typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!,
                getValue,
                System.Linq.Expressions.Expression.Constant(propertyType, typeof(Type)));
            valueExpression = System.Linq.Expressions.Expression.Convert(changeType, propertyType);
        }

        var assign = System.Linq.Expressions.Expression.Assign(
            System.Linq.Expressions.Expression.Property(target, property),
            valueExpression);

        return System.Linq.Expressions.Expression.Lambda<Action<TEntity, IDataRecord, int>>(
            assign, target, record, index).Compile();
    }

    private static bool IsNonNullable(Type type)
    {
        return type.IsValueType && Nullable.GetUnderlyingType(type) is null;
    }
}
