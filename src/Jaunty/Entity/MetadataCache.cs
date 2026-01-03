using System.Data;
using System.Linq.Expressions;
using System.Reflection;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

using Jaunty.Entity;

namespace Jaunty.Internal.Mapping;

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

    public static PropertySetter<T>[] GetSetters(IDataReader reader, MappingMode mode)
    {
        int fieldCount = reader.FieldCount;
        var setters = new List<PropertySetter<T>>(fieldCount);

        for (int i = 0; i < fieldCount; i++)
        {
            string columnName = reader.GetName(i);

            if (!ColumnToIndex.TryGetValue(columnName, out int propIndex))
            {
                if (mode == MappingMode.Strict)
                    throw new IndexOutOfRangeException($"Column '{columnName}' does not map to any property on {typeof(T).Name}.");

                continue;
            }

            setters.Add(new PropertySetter<T>(Properties[propIndex], i));
        }

        return [.. setters];
    }

    private static Action<T, IDataRecord, int> CreateSetter(PropertyInfo property)
    {
        ParameterExpression target = Expression.Parameter(typeof(T), "target");
        ParameterExpression record = Expression.Parameter(typeof(IDataRecord), "record");
        ParameterExpression index = Expression.Parameter(typeof(int), "index");
        MethodCallExpression getValue = Expression.Call(record, typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!, index);
        UnaryExpression converted = Expression.Convert(getValue, property.PropertyType);
        BinaryExpression assign = Expression.Assign(Expression.Property(target, property), converted);
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
