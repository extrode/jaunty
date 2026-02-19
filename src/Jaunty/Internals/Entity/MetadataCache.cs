using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

using Jaunty.Internals.Enums;
namespace Jaunty.Internals.Entity;

internal static class MetadataCache<T>
{
    public static readonly EntityMetadata Metadata;
    internal static readonly PropertyContext<T>[] Properties;
    private static readonly ConcurrentDictionary<string, PropertySetter<T>[]> SettersCache = new(StringComparer.Ordinal);

#if NET8_0_OR_GREATER
    private static readonly FrozenDictionary<string, int> ColumnToIndex;
#else
    private static readonly Dictionary<string, int> ColumnToIndex;
#endif

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

        Properties = [.. contexts];

#if NET8_0_OR_GREATER
        ColumnToIndex = nameToIndex.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
#else
        ColumnToIndex = nameToIndex;
#endif
    }

    internal static PropertySetter<T>[] GetSetters(IDataReader reader, MappingMode mode)
    {
        int fieldCount = reader.FieldCount;

        // Handle empty result sets
        if (fieldCount == 0)
            return [];

        string signature = GetReaderSignature(reader, mode);
        return SettersCache.GetOrAdd(signature, _ => BuildSetters(reader, mode));
    }

    private static string GetReaderSignature(IDataReader reader, MappingMode mode)
    {
        var sb = new StringBuilder();
        sb.Append((int)mode).Append('|').Append(reader.FieldCount);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            sb.Append('|').Append(reader.GetName(i));
        }
        return sb.ToString();
    }

    private static PropertySetter<T>[] BuildSetters(IDataReader reader, MappingMode mode)
    {
        int fieldCount = reader.FieldCount;
        var settersBuffer = new PropertySetter<T>[fieldCount];
        int count = 0;

#if NET8_0_OR_GREATER
        Span<bool> matchedProperties = stackalloc bool[Properties.Length];
#else
        var matchedProperties = new bool[Properties.Length];
#endif

        for (int i = 0; i < fieldCount; i++)
        {
            string columnName;
            try
            {
                columnName = reader.GetName(i) ?? throw new InvalidOperationException($"Column {i} has no name");
            }
            catch (NullReferenceException)
            {
                // Handle SQLite async DataReader limitation: use position-based mapping
                if (i < Properties.Length)
                {
                    settersBuffer[count++] = new PropertySetter<T>(Properties[i], i);
                    matchedProperties[i] = true;
                    continue;
                }
                continue;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get column name for field {i}", ex);
            }

            if (ColumnToIndex.TryGetValue(columnName, out int propIndex))
            {
                if (matchedProperties[propIndex])
                {
                    if (mode == MappingMode.Strict)
                    {
                        var prop = Properties[propIndex];
                        throw new InvalidOperationException(
                            $"Strict mapping failed: Property '{prop.Property.Name}' was mapped more than once from the result set.");
                    }

                    continue;
                }

                settersBuffer[count++] = new PropertySetter<T>(Properties[propIndex], i);
                matchedProperties[propIndex] = true;
            }
            else if (mode == MappingMode.Strict)
            {
                throw new InvalidOperationException(
                    $"Mapping failed: Column '{columnName}' in the result set does not map to any property of type '{typeof(T).FullName}'.");
            }
        }

        if (mode == MappingMode.Strict)
        {
            for (int i = 0; i < matchedProperties.Length; i++)
            {
                if (!matchedProperties[i])
                {
                    var prop = Properties[i];
                    throw new InvalidOperationException(
                        $"Strict mapping failed: Property '{prop.Property.Name}' (mapped to column '{prop.ColumnName}') was missing from the result set.");
                }
            }
        }

        if (count == settersBuffer.Length)
            return settersBuffer;

        var result = new PropertySetter<T>[count];
        Array.Copy(settersBuffer, result, count);
        return result;
    }

    private static Action<T, IDataRecord, int> CreateSetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var record = Expression.Parameter(typeof(IDataRecord), "record");
        var index = Expression.Parameter(typeof(int), "index");

        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var isEnum = underlyingType.IsEnum;
        var conversionType = isEnum ? Enum.GetUnderlyingType(underlyingType) : underlyingType;

        var getValue = Expression.Call(record, typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!, index);

        Expression valueExpression;

        if (conversionType == typeof(string))
        {
            valueExpression = Expression.Convert(getValue, typeof(string));
        }
        else if (conversionType == typeof(int))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToInt32), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(long))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToInt64), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(bool))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToBoolean), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(DateTime))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToDateTime), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(decimal))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToDecimal), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(double))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToDouble), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(float))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToSingle), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(short))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToInt16), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(byte))
        {
            valueExpression = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ToByte), [typeof(object)])!, getValue);
        }
        else if (conversionType == typeof(Guid))
        {
            valueExpression = Expression.Call(typeof(MetadataCache<T>).GetMethod(nameof(ParseGuid), BindingFlags.NonPublic | BindingFlags.Static)!, getValue);
        }
        else
        {
            var changeType = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!, getValue,
                Expression.Constant(conversionType, typeof(Type)));
            valueExpression = Expression.Convert(changeType, conversionType);
        }

        if (isEnum)
        {
            valueExpression = Expression.Convert(valueExpression, underlyingType);
        }

        if (propertyType != underlyingType)
        {
            valueExpression = Expression.Convert(valueExpression, propertyType);
        }

        var assign = Expression.Assign(Expression.Property(target, property), valueExpression);
        return Expression.Lambda<Action<T, IDataRecord, int>>(assign, target, record, index).Compile();
    }

    private static Action<T, DbDataReader, int> CreateFastSetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var reader = Expression.Parameter(typeof(DbDataReader), "reader");
        var index = Expression.Parameter(typeof(int), "index");

        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var isEnum = underlyingType.IsEnum;
        var conversionType = isEnum ? Enum.GetUnderlyingType(underlyingType) : underlyingType;

        // Fast path: reader.GetFieldValue<T>(index)
        var getFieldValueMethod = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!.MakeGenericMethod(conversionType);
        var getValue = Expression.Call(reader, getFieldValueMethod, index);

        Expression valueExpression = getValue;

        if (isEnum)
        {
            valueExpression = Expression.Convert(valueExpression, underlyingType);
        }

        if (propertyType != underlyingType)
        {
            valueExpression = Expression.Convert(valueExpression, propertyType);
        }

        var assign = Expression.Assign(Expression.Property(target, property), valueExpression);
        return Expression.Lambda<Action<T, DbDataReader, int>>(assign, target, reader, index).Compile();
    }

    private static Func<T, object?> CreateGetter(PropertyInfo property)
    {
        var target = Expression.Parameter(typeof(T), "target");
        var access = Expression.Property(target, property);
        var box = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<T, object?>>(box, target).Compile();
    }

    private static Guid ParseGuid(object value)
    {
        if (value is Guid g) return g;
        if (value is string s) return Guid.Parse(s);
        if (value is byte[] b) return new Guid(b);
        return (Guid)Convert.ChangeType(value, typeof(Guid));
    }


    private static bool IsNonNullableType(Type type)
    {
        return type.IsValueType && Nullable.GetUnderlyingType(type) is null;
    }
}

internal readonly struct PropertyContext<T>(PropertyInfo property, Action<T, IDataRecord, int> setter, Action<T, DbDataReader, int> fastSetter, Func<T, object?> getter, string propertyName, string columnName, bool isNonNullable)
{
    public PropertyInfo Property { get; } = property;
    public Action<T, IDataRecord, int> Setter { get; } = setter;
    public Action<T, DbDataReader, int> FastSetter { get; } = fastSetter;
    public Func<T, object?> Getter { get; } = getter;
    public string PropertyName { get; } = propertyName;
    public string ColumnName { get; } = columnName;
    public bool IsNonNullable { get; } = isNonNullable;
}

internal readonly struct PropertySetter<T>(PropertyContext<T> context, int ordinal)
{
    public void Set(T target, IDataRecord record)
    {
        if (!record.IsDBNull(ordinal))
            context.Setter(target, record, ordinal);
        else if (context.IsNonNullable)
            throw new InvalidOperationException(
                $"Cannot assign NULL to non-nullable property '{context.Property.Name}' on type '{typeof(T).Name}'.");
    }

    public void SetFast(T target, DbDataReader reader)
    {
        if (!reader.IsDBNull(ordinal))
            context.FastSetter(target, reader, ordinal);
        else if (context.IsNonNullable)
            throw new InvalidOperationException(
                $"Cannot assign NULL to non-nullable property '{context.Property.Name}' on type '{typeof(T).Name}'.");
    }
}
