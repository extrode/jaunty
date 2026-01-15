using System.Data;
using System.Linq.Expressions;
using System.Reflection;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

using Jaunty.Internals.Enums;
namespace Jaunty.Internals.Entity;

internal static class MetadataCache<T> where T : new()
{
    public static readonly EntityMetadata Metadata;
    private static readonly PropertyContext<T>[] Properties;

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
            var isNonNullable = IsNonNullableType(column.Property.PropertyType);

            contexts.Add(new PropertyContext<T>(column.Property, setter, column.Property.Name, column.ColumnName, isNonNullable));

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
                // When GetName() fails, map by position order instead of column name
                if (i < Properties.Length)
                {
                    settersBuffer[count++] = new PropertySetter<T>(Properties[i], i);
                    matchedProperties[i] = true;
                    continue;
                }
                // If we get here, we have more columns than properties
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

        var getValue = Expression.Call(record, typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!, index);

        var propertyType = property.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propertyType);

        Expression valueExpression;

        if (underlyingType is not null)
        {
            // Nullable<T>: (T?)Convert.ChangeType(value, typeof(T))
            var changeType = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!, getValue,
                Expression.Constant(underlyingType, typeof(Type)));
            valueExpression = Expression.Convert(changeType, propertyType);
        }
        else
        {
            // Non-nullable: Convert.ChangeType(value, propertyType)
            var changeType = Expression.Call(typeof(Convert).GetMethod(nameof(Convert.ChangeType), [typeof(object), typeof(Type)])!, getValue,
                Expression.Constant(propertyType, typeof(Type)));
            valueExpression = Expression.Convert(changeType, propertyType);
        }

        var assign = Expression.Assign(Expression.Property(target, property), valueExpression);
        return Expression.Lambda<Action<T, IDataRecord, int>>(assign, target, record, index).Compile();
    }


    private static bool IsNonNullableType(Type type)
    {
        return type.IsValueType && Nullable.GetUnderlyingType(type) is null;
    }
}

internal readonly struct PropertyContext<T>(PropertyInfo property, Action<T, IDataRecord, int> setter, string propertyName, string columnName, bool isNonNullable)
{
    public PropertyInfo Property { get; } = property;
    public Action<T, IDataRecord, int> Setter { get; } = setter;
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
}
