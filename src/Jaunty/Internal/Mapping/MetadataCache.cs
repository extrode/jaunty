using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Jaunty.Enums;
using Jaunty.Internal;

namespace Jaunty.Internal.Mapping;

internal static class MetadataCache<T> where T : new()
{
    private static readonly PropertyMeta[] Properties;
    private static readonly Dictionary<string, Func<int, Action<T, IDataReader>>> SetterFactories;

    static MetadataCache()
    {
        var type = typeof(T);
        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var writableProps = new List<PropertyMeta>(props.Length);
        var factories = new Dictionary<string, Func<int, Action<T, IDataReader>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in props)
        {
            if (!p.CanWrite) continue;
            if (NameResolver.IsIgnored(p)) continue;

            var columnName = NameResolver.GetColumnName(p);
            var meta = new PropertyMeta(p, columnName);
            writableProps.Add(meta);
            factories[columnName] = CreateSetterFactory(p);
        }

        Properties = [.. writableProps];
        SetterFactories = factories;
    }

    public static ColumnSetter<T>[] GetSetters(IDataReader reader, MappingMode mode)
    {
        var columnCount = reader.FieldCount;
        var readerColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < columnCount; i++)
            readerColumns.Add(reader.GetName(i));

        if (mode == MappingMode.Strict)
            ValidateStrictMode(readerColumns);

        var setters = new List<ColumnSetter<T>>(columnCount);

        for (int i = 0; i < columnCount; i++)
        {
            var columnName = reader.GetName(i);

            if (SetterFactories.TryGetValue(columnName, out var factory))
                setters.Add(new ColumnSetter<T>(i, factory(i)));
        }

        return [.. setters];
    }

    public static string GetTableName() => NameResolver.GetTableName(typeof(T));

    public static string[] GetColumnNames() => [.. Properties.Select(p => p.ColumnName)];

    private static void ValidateStrictMode(HashSet<string> readerColumns)
    {
        for (int i = 0; i < Properties.Length; i++)
        {
            var prop = Properties[i];

            if (!readerColumns.Contains(prop.ColumnName))
                throw new InvalidOperationException($"Strict mapping failed: property '{prop.Property.Name}' (column '{prop.ColumnName}') on type '{typeof(T).Name}' has no matching column in the result set.");
        }
    }

    private static Func<int, Action<T, IDataReader>> CreateSetterFactory(PropertyInfo prop)
    {
        return ordinal =>
        {
            var obj = Expression.Parameter(typeof(T), "obj");
            var reader = Expression.Parameter(typeof(IDataReader), "r");

            var ordinalConst = Expression.Constant(ordinal);

            var isDbNull = Expression.Call(
                reader,
                typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!,
                ordinalConst);

            var getValue = Expression.Call(
                reader,
                typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!,
                ordinalConst);

            var propType = prop.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propType);
            var isNullable = underlyingType != null || !propType.IsValueType;

            Expression assignValue;

            if (isNullable)
            {
                var converted = Expression.Convert(getValue, propType);
                var defaultValue = Expression.Default(propType);

                assignValue = Expression.Condition(isDbNull, defaultValue, converted);
            }
            else
            {
                var throwExpr = Expression.Throw(
                    Expression.New(
                        typeof(InvalidOperationException).GetConstructor([typeof(string)])!,
                        Expression.Constant($"Cannot assign NULL to non-nullable property '{prop.Name}' on type '{typeof(T).Name}'.")),
                    propType);

                var converted = Expression.Convert(getValue, propType);
                assignValue = Expression.Condition(isDbNull, throwExpr, converted);
            }

            var assign = Expression.Assign(
                Expression.Property(obj, prop),
                assignValue);

            return Expression.Lambda<Action<T, IDataReader>>(assign, obj, reader).Compile();
        };
    }

    private readonly struct PropertyMeta(PropertyInfo property, string columnName)
    {
        public readonly PropertyInfo Property = property;
        public readonly string ColumnName = columnName;
    }
}
