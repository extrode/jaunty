using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;

namespace Jaunty.Extensions.Reflection;

public static class MetadataCache<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
    T>
{
    public static readonly EntityMetadata Metadata;
    public static readonly PropertyContext<T>[] Properties;
    private static readonly ConcurrentDictionary<ReaderSignature, PropertySetter<T>[]> SettersCache = new();

    private static readonly Dictionary<string, int> ColumnToIndex;

    static MetadataCache()
    {
        Metadata = MetadataBuilder.Build<T>();
        var columns = Metadata.Columns.ToArray();
        var contexts = new List<PropertyContext<T>>(columns.Length);
        var nameToIndex = new Dictionary<string, int>(columns.Length, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < columns.Length; i++)
        {
            var column = columns[i];
            var setter = CreateSetter(column.Property);
            var fastSetter = CreateFastSetter(column.Property);
            var getter = CreateGetter(column.Property);
            var isNonNullable = IsNonNullableType(column.Property.PropertyType);

            contexts.Add(new PropertyContext<T>(column.Property, setter, fastSetter, getter, column.Property.Name, column.ColumnName, isNonNullable));

            nameToIndex[column.ColumnName] = i;

            if (!column.ColumnName.Equals(column.Property.Name, StringComparison.OrdinalIgnoreCase))
                nameToIndex[column.Property.Name] = i;
        }

        Properties = contexts.ToArray();
        ColumnToIndex = nameToIndex;
    }

    public static PropertySetter<T>[] GetSetters(IDataReader reader, MappingMode mode)
    {
        int fieldCount = reader.FieldCount;
        if (fieldCount == 0) return Array.Empty<PropertySetter<T>>();

        var signature = new ReaderSignature(reader, mode);
        if (SettersCache.TryGetValue(signature, out var cached)) return cached;
        
        var setters = BuildSetters(reader, mode);
        SettersCache.TryAdd(signature, setters);
        return setters;
    }

    private readonly struct ReaderSignature : IEquatable<ReaderSignature>
    {
        private readonly int _hashCode;

        public ReaderSignature(IDataReader reader, MappingMode mode)
        {
            int fieldCount = reader.FieldCount;
            int h = 17;
            h = h * 31 + (int)mode;
            h = h * 31 + fieldCount;
            for (int i = 0; i < fieldCount; i++)
            {
                var name = reader.GetName(i);
                h = h * 31 + (name?.GetHashCode() ?? 0);
            }
            _hashCode = h;
        }

        public bool Equals(ReaderSignature other) => _hashCode == other._hashCode;
        public override bool Equals(object? obj) => obj is ReaderSignature other && Equals(other);
        public override int GetHashCode() => _hashCode;
    }

    private static PropertySetter<T>[] BuildSetters(IDataReader reader, MappingMode mode)
    {
        int fieldCount = reader.FieldCount;
        var settersBuffer = new PropertySetter<T>[fieldCount];
        int count = 0;
        var matchedProperties = new bool[Properties.Length];

        for (int i = 0; i < fieldCount; i++)
        {
            string columnName = reader.GetName(i) ?? throw new InvalidOperationException($"Column {i} has no name");

            if (ColumnToIndex.TryGetValue(columnName, out int propIndex))
            {
                if (matchedProperties[propIndex]) continue;

                settersBuffer[count++] = new PropertySetter<T>(Properties[propIndex], i);
                matchedProperties[propIndex] = true;
            }
            else if (mode == MappingMode.Strict)
            {
                throw new InvalidOperationException($"Mapping failed: Column '{columnName}' does not map to any property of type '{typeof(T).FullName}'.");
            }
        }

        // In strict mode, verify all properties have matching columns
        if (mode == MappingMode.Strict)
        {
            for (int i = 0; i < matchedProperties.Length; i++)
            {
                if (!matchedProperties[i])
                {
                    throw new InvalidOperationException($"Strict mapping failed: property '{Properties[i].Property.Name}' has no matching column in result set for type '{typeof(T).FullName}'.");
                }
            }
        }

        var result = new PropertySetter<T>[count];
        Array.Copy(settersBuffer, result, count);
        return result;
    }

    private static Action<T, IDataRecord, int> CreateSetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var record = Expression.Parameter(typeof(IDataRecord), "record");
        var index = Expression.Parameter(typeof(int), "index");
        var getValue = Expression.Call(record, typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!, index);
        
        var propertyType = property.PropertyType;
        var conversionType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        
        Expression valueExpression = Expression.Convert(
            Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ChangeType), new[] { typeof(object), typeof(Type) })!, 
            getValue, Expression.Constant(conversionType)), conversionType);

        if (propertyType != conversionType)
            valueExpression = Expression.Convert(valueExpression, propertyType);

        var assign = Expression.Assign(Expression.Property(target, property), valueExpression);
        return Expression.Lambda<Action<T, IDataRecord, int>>(assign, target, record, index).Compile();
    }

    private static Action<T, DbDataReader, int> CreateFastSetter(PropertyInfo property)
    {
        var standard = CreateSetter(property);
        return (target, reader, index) => standard(target, reader, index);
    }

    private static Func<T, object?> CreateGetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var access = Expression.Property(target, property);
        var box = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<T, object?>>(box, target).Compile();
    }

    private static bool IsNonNullableType(Type type) => type.IsValueType && Nullable.GetUnderlyingType(type) is null;
}

public readonly struct PropertyContext<T>(PropertyInfo property, Action<T, IDataRecord, int> setter, Action<T, DbDataReader, int> fastSetter, Func<T, object?> getter, string propertyName, string columnName, bool isNonNullable)
{
    public PropertyInfo Property { get; } = property;
    public Action<T, IDataRecord, int> Setter { get; } = setter;
    public Func<T, object?> Getter { get; } = getter;
    public string PropertyName { get; } = propertyName;
    public string ColumnName { get; } = columnName;
    public bool IsNonNullable { get; } = isNonNullable;
}

public readonly struct PropertySetter<T>(PropertyContext<T> context, int ordinal)
{
    public int Ordinal { get; } = ordinal;
    
    public void Set(T target, IDataRecord record)
    {
        if (!record.IsDBNull(ordinal))
            context.Setter(target, record, ordinal);
        else if (context.IsNonNullable)
            throw new InvalidOperationException($"Cannot assign NULL to non-nullable property '{context.PropertyName}'.");
    }
}
