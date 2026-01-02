using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Jaunty.Internal;

namespace Jaunty.Internal.Mapping;

internal static class MetadataCache<T> where T : new()
{
    // Cached MethodInfo for IDataRecord - avoids repeated reflection
    private static readonly MethodInfo IsDbNullMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo GetValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly ConstructorInfo InvalidOpExCtor = typeof(InvalidOperationException).GetConstructor([typeof(string)])!;

    private static readonly PropertyMeta[] Properties;
    private static readonly Dictionary<string, Func<int, Action<T, IDataReader>>> SetterFactories;
    private static readonly string[] ColumnNames;

    static MetadataCache()
    {
        var type = typeof(T);
        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var writableProps = new List<PropertyMeta>(props.Length);
        var factories = new Dictionary<string, Func<int, Action<T, IDataReader>>>(props.Length, StringComparer.OrdinalIgnoreCase);

        foreach (var p in props)
        {
            if (!p.CanWrite) continue;
            if (NameResolver.IsIgnored(p)) continue;

            var columnName = NameResolver.GetColumnName(p);
            writableProps.Add(new PropertyMeta(p, columnName));
            factories[columnName] = CreateSetterFactory(p);
        }

        Properties = writableProps.ToArray();
        SetterFactories = factories;

        // Pre-compute column names array
        ColumnNames = new string[Properties.Length];
        for (int i = 0; i < Properties.Length; i++)
            ColumnNames[i] = Properties[i].ColumnName;
    }

    public static ColumnSetter<T>[] GetSetters(IDataReader reader, MappingMode mode)
    {
        var columnCount = reader.FieldCount;

        if (mode == MappingMode.Strict)
            ValidateStrictMode(reader, columnCount);

        // Pre-size array to maximum possible size
        var setters = new ColumnSetter<T>[columnCount];
        var setterCount = 0;

        for (int i = 0; i < columnCount; i++)
        {
            var columnName = reader.GetName(i);

            if (SetterFactories.TryGetValue(columnName, out var factory))
                setters[setterCount++] = new ColumnSetter<T>(i, factory(i));
        }

        // Return exact-sized array
        if (setterCount == columnCount)
            return setters;

        var result = new ColumnSetter<T>[setterCount];
        Array.Copy(setters, result, setterCount);
        return result;
    }

    public static string GetTableName() => NameResolver.GetTableName(typeof(T));

    public static string[] GetColumnNames() => ColumnNames;

    private static void ValidateStrictMode(IDataReader reader, int columnCount)
    {
        // Build set of reader columns
        var readerColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columnCount; i++)
            readerColumns.Add(reader.GetName(i));

        // Check all properties exist
        for (int i = 0; i < Properties.Length; i++)
        {
            var prop = Properties[i];
            if (!readerColumns.Contains(prop.ColumnName))
                throw new InvalidOperationException($"Strict mapping failed: property '{prop.Property.Name}' (column '{prop.ColumnName}') on type '{typeof(T).Name}' has no matching column in the result set.");
        }
    }

    private static Func<int, Action<T, IDataReader>> CreateSetterFactory(PropertyInfo prop)
    {
        var propType = prop.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propType);
        var isNullable = underlyingType != null || !propType.IsValueType;
        var errorMessage = isNullable ? null : $"Cannot assign NULL to non-nullable property '{prop.Name}' on type '{typeof(T).Name}'.";

        return ordinal =>
        {
            var obj = Expression.Parameter(typeof(T), "obj");
            var reader = Expression.Parameter(typeof(IDataReader), "r");
            var ordinalConst = Expression.Constant(ordinal);

            var isDbNull = Expression.Call(reader, IsDbNullMethod, ordinalConst);
            var getValue = Expression.Call(reader, GetValueMethod, ordinalConst);

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
                    Expression.New(InvalidOpExCtor, Expression.Constant(errorMessage)),
                    propType);
                var converted = Expression.Convert(getValue, propType);
                assignValue = Expression.Condition(isDbNull, throwExpr, converted);
            }

            var assign = Expression.Assign(Expression.Property(obj, prop), assignValue);
            return Expression.Lambda<Action<T, IDataReader>>(assign, obj, reader).Compile();
        };
    }

    private readonly struct PropertyMeta(PropertyInfo property, string columnName)
    {
        public readonly PropertyInfo Property = property;
        public readonly string ColumnName = columnName;
    }
}
