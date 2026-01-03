using System.Data;
using System.Linq.Expressions;
using System.Reflection;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace Jaunty.Entity;

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
        IReadOnlyList<ColumnMetadata> columns = Metadata.Columns;
        var contexts = new List<PropertyContext<T>>(columns.Count);
        var nameToIndex = new Dictionary<string, int>(columns.Count, StringComparer.OrdinalIgnoreCase);
        int ordinal = 0;

        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata column = columns[i];
            var setter = CreateSetter(column.Property);
            contexts.Add(new PropertyContext<T>(column.Property, setter, column.ColumnName));
            nameToIndex[column.ColumnName] = ordinal++;
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
        var settersBuffer = new PropertySetter<T>[fieldCount];
        int count = 0;

#if NET8_0_OR_GREATER
    Span<bool> matchedProperties = stackalloc bool[Properties.Length];
#else
        var matchedProperties = new bool[Properties.Length];
#endif

        for (int i = 0; i < fieldCount; i++)
        {
            string columnName = reader.GetName(i);

            if (ColumnToIndex.TryGetValue(columnName, out int propIndex))
            {
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

        if (count == fieldCount) return settersBuffer;
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
        var converted = Expression.Convert(getValue, property.PropertyType);
        var assign = Expression.Assign(Expression.Property(target, property), converted);
        return Expression.Lambda<Action<T, IDataRecord, int>>(assign, target, record, index).Compile();
    }
}

internal readonly struct PropertyContext<T>(PropertyInfo property, Action<T, IDataRecord, int> setter, string columnName)
{
    public PropertyInfo Property { get; } = property;
    public Action<T, IDataRecord, int> Setter { get; } = setter;
    public string ColumnName { get; } = columnName;
}


internal readonly struct PropertySetter<T>(PropertyContext<T> context, int ordinal)
{
    private readonly PropertyContext<T> _context = context;
    private readonly int _ordinal = ordinal;

    public void Set(T target, IDataRecord record)
    {
        if (!record.IsDBNull(_ordinal))
            _context.Setter(target, record, _ordinal);
    }
}
