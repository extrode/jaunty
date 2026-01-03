using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

// Modern-only namespaces
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
using System.Runtime.InteropServices;
#endif

namespace Jaunty.Internal.Mapping;

internal static class MetadataCacheOld<T> where T : new()
{
    // ---------------------------------------------------------
    // 1. Conditional Collections
    // ---------------------------------------------------------
#if NET8_0_OR_GREATER
    private static readonly FrozenDictionary<Type, MethodInfo> TypedGetters;
    private static readonly FrozenDictionary<string, int> ColumnToIndex;
#else
    private static readonly Dictionary<Type, MethodInfo> TypedGetters;
    private static readonly Dictionary<string, int> ColumnToIndex;
#endif

    private static readonly MethodInfo IsDbNullMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.IsDBNull))!;
    private static readonly MethodInfo GetValueMethod = typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!;
    private static readonly ConstructorInfo InvalidOpExCtor = typeof(InvalidOperationException).GetConstructor([typeof(string)])!;

    private readonly struct PropertyContext
    {
        public readonly PropertyInfo Property;
        public readonly Func<int, Action<T, IDataReader>> Factory;
        public readonly string ColumnName;

        public PropertyContext(PropertyInfo p, Func<int, Action<T, IDataReader>> f, string c)
        {
            Property = p; Factory = f; ColumnName = c;
        }
    }

    private static readonly PropertyContext[] Properties;
    private static readonly string[] ColumnNames;
    private static readonly string TableName;

    static MetadataCacheOld()
    {
        var type = typeof(T);
        TableName = NameResolver.GetTableName(type);

        // ---------------------------------------------------------
        // 2. Build Typed Getters Map (Shared Logic)
        // ---------------------------------------------------------
        var getters = new Dictionary<Type, MethodInfo>
        {
            { typeof(bool), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetBoolean))! },
            { typeof(byte), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetByte))! },
            { typeof(char), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetChar))! },
            { typeof(short), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt16))! },
            { typeof(int), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt32))! },
            { typeof(long), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetInt64))! },
            { typeof(float), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetFloat))! },
            { typeof(double), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDouble))! },
            { typeof(decimal), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDecimal))! },
            { typeof(Guid), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetGuid))! },
            { typeof(DateTime), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetDateTime))! },
            { typeof(string), typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetString))! }
        };

        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var contexts = new List<PropertyContext>(props.Length);
        var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        int validPropIndex = 0;
        foreach (var p in props)
        {
            if (!p.CanWrite || NameResolver.IsIgnored(p)) continue;

            var columnName = NameResolver.GetColumnName(p);
            var factory = CreateSetterFactory(p, getters); // Pass getters map to factory helper

            contexts.Add(new PropertyContext(p, factory, columnName));
            nameToIndex[columnName] = validPropIndex++;
        }

        Properties = contexts.ToArray();

        ColumnNames = new string[Properties.Length];
        for (int i = 0; i < Properties.Length; i++)
            ColumnNames[i] = Properties[i].ColumnName;

        // ---------------------------------------------------------
        // 3. Freeze Collections for NET8+
        // ---------------------------------------------------------
#if NET8_0_OR_GREATER
        TypedGetters = getters.ToFrozenDictionary();
        ColumnToIndex = nameToIndex.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
#else
        TypedGetters = getters;
        ColumnToIndex = nameToIndex;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetTableName() => TableName;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string[] GetColumnNames() => ColumnNames;

    public static ColumnSetter<T>[] GetSetters(IDataReader reader, MappingMode mode)
    {
        int fieldCount = reader.FieldCount;
        var settersBuffer = new ColumnSetter<T>[fieldCount];
        int settersCount = 0;

        // ---------------------------------------------------------
        // 4. Allocation Strategy: Stack vs Heap
        // ---------------------------------------------------------
#if NET8_0_OR_GREATER
        // NET 8: Use Stack Memory (Zero Allocation)
        Span<bool> matchedProps = stackalloc bool[Properties.Length];
#else
        // Standard 2.0: Use Heap Memory (Safe Fallback)
        var matchedProps = new bool[Properties.Length];
#endif

        for (int ordinal = 0; ordinal < fieldCount; ordinal++)
        {
            var columnName = reader.GetName(ordinal);

            if (ColumnToIndex.TryGetValue(columnName, out int propIndex))
            {
                // In NET8 this struct access is slightly faster due to readonly ref
                var ctx = Properties[propIndex];

                settersBuffer[settersCount++] = new ColumnSetter<T>(ordinal, ctx.Factory(ordinal));

                if (mode == MappingMode.Strict)
                {
                    matchedProps[propIndex] = true;
                }
            }
        }

        if (mode == MappingMode.Strict)
        {
#if NET8_0_OR_GREATER
            ValidateStrictMode(matchedProps);
#else
            // Pass array as ReadOnlySpan or just array for legacy
            ValidateStrictMode(matchedProps);
#endif
        }

        if (settersCount == fieldCount)
            return settersBuffer;

        // ---------------------------------------------------------
        // 5. Array Creation Strategy
        // ---------------------------------------------------------
#if NET8_0_OR_GREATER
        var result = GC.AllocateUninitializedArray<ColumnSetter<T>>(settersCount);
#else
        var result = new ColumnSetter<T>[settersCount];
#endif
        Array.Copy(settersBuffer, result, settersCount);
        return result;
    }

    // Accepts Span on NET8 (can take stackalloc), Array on Legacy
#if NET8_0_OR_GREATER
    private static void ValidateStrictMode(ReadOnlySpan<bool> matchedProps)
#else
    private static void ValidateStrictMode(bool[] matchedProps)
#endif
    {
        for (int i = 0; i < matchedProps.Length; i++)
        {
            if (!matchedProps[i])
            {
                var prop = Properties[i];
                throw new InvalidOperationException(
                    $"Strict mapping failed: property '{prop.Property.Name}' (column '{prop.ColumnName}') on type '{typeof(T).Name}' has no matching column in the result set.");
            }
        }
    }

    private static Func<int, Action<T, IDataReader>> CreateSetterFactory(PropertyInfo prop, Dictionary<Type, MethodInfo> gettersMap)
    {
        var propType = prop.PropertyType;
        var underlyingType = Nullable.GetUnderlyingType(propType);
        var targetType = underlyingType ?? propType;
        var isNullable = underlyingType != null || !propType.IsValueType;

        var errorMessage = isNullable ? null : $"Cannot assign NULL to non-nullable property '{prop.Name}' on type '{typeof(T).Name}'.";

        return ordinal =>
        {
            var objParam = Expression.Parameter(typeof(T), "obj");
            var readerParam = Expression.Parameter(typeof(IDataReader), "r");
            var ordinalConst = Expression.Constant(ordinal, typeof(int));

            var isDbNullCall = Expression.Call(readerParam, IsDbNullMethod, ordinalConst);
            Expression valueExpression;

            if (gettersMap.TryGetValue(targetType, out var getterMethod))
            {
                valueExpression = Expression.Call(readerParam, getterMethod, ordinalConst);
            }
            else
            {
                var getValueCall = Expression.Call(readerParam, GetValueMethod, ordinalConst);
                valueExpression = Expression.Convert(getValueCall, targetType);
            }

            Expression finalValue;
            if (isNullable)
            {
                var defaultValue = Expression.Default(propType);
                var convertedValue = Expression.Convert(valueExpression, propType);
                finalValue = Expression.Condition(isDbNullCall, defaultValue, convertedValue);
            }
            else
            {
                var throwExpr = Expression.Block(
                    Expression.Throw(Expression.New(InvalidOpExCtor, Expression.Constant(errorMessage))),
                    Expression.Default(propType)
                );
                finalValue = Expression.Condition(isDbNullCall, throwExpr, valueExpression);
            }

            var assign = Expression.Assign(Expression.Property(objParam, prop), finalValue);
            return Expression.Lambda<Action<T, IDataReader>>(assign, objParam, readerParam).Compile();
        };
    }
}