using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Jaunty.Internals.Entity;

namespace Jaunty;

/// <summary>
/// Builds and caches column-to-property mappings for two entity types.
/// Uses property-name matching: each column is mapped to the first type that has a matching property.
/// </summary>
internal sealed class MultiEntityMapper<T1, T2> where T1 : new() where T2 : new()
{
    private readonly (int Ordinal, Action<T1, IDataRecord, int> Setter)[] _t1Setters;
    private readonly (int Ordinal, Action<T2, IDataRecord, int> Setter)[] _t2Setters;
    private readonly (int Ordinal, bool IsNonNullable, string PropertyName)[] _t1NullChecks;
    private readonly (int Ordinal, bool IsNonNullable, string PropertyName)[] _t2NullChecks;

    private MultiEntityMapper((int, Action<T1, IDataRecord, int>)[] t1Setters, (int, Action<T2, IDataRecord, int>)[] t2Setters, (int, bool, string)[] t1NullChecks, (int, bool, string)[] t2NullChecks)
    {
        _t1Setters = t1Setters;
        _t2Setters = t2Setters;
        _t1NullChecks = t1NullChecks;
        _t2NullChecks = t2NullChecks;
    }

    internal static MultiEntityMapper<T1, T2> Build(IDataReader reader)
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

        return new MultiEntityMapper<T1, T2>([.. t1Setters], [.. t2Setters], [.. t1NullChecks], [.. t2NullChecks]);
    }

    internal void ApplyT1(T1 target, IDataRecord record)
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

    internal void ApplyT2(T2 target, IDataRecord record)
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

    private static Action<TEntity, IDataRecord, int> CreateSetter<TEntity>(PropertyInfo property)
    {
        ParameterExpression target = Expression.Parameter(typeof(TEntity), "target");
        ParameterExpression record = Expression.Parameter(typeof(IDataRecord), "record");
        ParameterExpression index = Expression.Parameter(typeof(int), "index");
        MethodCallExpression getValue = Expression.Call(record, typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!, index);
        Type propertyType = property.PropertyType;
        Type? underlyingType = Nullable.GetUnderlyingType(propertyType);

        Expression valueExpression;

        if (underlyingType is not null)
        {
            var changeType = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!, getValue, Expression.Constant(underlyingType, typeof(Type)));
            valueExpression = Expression.Convert(changeType, propertyType);
        }
        else
        {
            var changeType = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!, getValue, Expression.Constant(propertyType, typeof(Type)));
            valueExpression = Expression.Convert(changeType, propertyType);
        }

        var assign = Expression.Assign(Expression.Property(target, property), valueExpression);
        return Expression.Lambda<Action<TEntity, IDataRecord, int>>(assign, target, record, index).Compile();
    }

    private static bool IsNonNullable(Type type)
    {
        return type.IsValueType && Nullable.GetUnderlyingType(type) is null;
    }
}
