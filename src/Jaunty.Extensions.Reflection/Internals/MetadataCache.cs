using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Internals.Entity;
using Jaunty.Attributes;
using Jaunty.TypeHandlers;
using System.Globalization;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Caches entity metadata and property mappings for reflection-based entity mapping.
/// </summary>
/// <remarks>
/// This cache uses runtime reflection and is not compatible with NativeAOT.
/// For NativeAOT scenarios, use the source generator instead.
/// </remarks>
internal static class MetadataCache<T>
{
    /// <summary>
    /// The cached entity metadata for type <typeparamref name="T"/>.
    /// </summary>
    public static readonly EntityMetadata Metadata;

    /// <summary>
    /// The cached property contexts for type <typeparamref name="T"/>.
    /// </summary>
    public static readonly PropertyContext<T>[] Properties;

    private static readonly ConcurrentDictionary<ReaderSignature, PropertySetter<T>[]> SettersCache = new();

    private static readonly Dictionary<string, int> ColumnToIndex;

    static MetadataCache()
    {
        Metadata = MetadataBuilder.Build<T>();
        ColumnMetadata[] columns = Metadata.Columns.ToArray();
        List<PropertyContext<T>> contexts = new List<PropertyContext<T>>(columns.Length);
        Dictionary<string, int> nameToIndex = new Dictionary<string, int>(columns.Length, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < columns.Length; i++)
        {
            ColumnMetadata column = columns[i];
            PropertyInfo property = column.Property!;
            Action<T, IDataRecord, int> setter = CreateSetter(property);
            Func<T, object?> getter = CreateGetter(property);
            bool isNonNullable = IsNonNullableType(property.PropertyType);

            contexts.Add(new PropertyContext<T>(property, setter, getter, column.PropertyName, column.ColumnName, isNonNullable));

            nameToIndex[column.ColumnName] = i;

            if (!column.ColumnName.Equals(column.PropertyName, StringComparison.OrdinalIgnoreCase))
                nameToIndex[column.PropertyName] = i;
        }

        Properties = contexts.ToArray();
        ColumnToIndex = nameToIndex;
    }

    /// <summary>
    /// Gets or builds property setters for mapping data from a reader to entity properties.
    /// </summary>
    /// <param name="reader">The data reader to build setters for.</param>
    /// <param name="mode">The mapping mode (strict or lenient).</param>
    /// <returns>An array of property setters matched to the reader's columns.</returns>
    public static PropertySetter<T>[] GetSetters(IDataReader reader, MappingMode mode)
    {
        int fieldCount = reader.FieldCount;
        if (fieldCount == 0) return Array.Empty<PropertySetter<T>>();

        // AUD-R26: GetTypedMapper<T>'s delegate maps one row to one entity, so this method runs
        // once per row of every reflection-mapped result set - and building a ReaderSignature
        // allocates a string[fieldCount + 2] and joins it before the lookup can even be attempted.
        // The same IDataReader instance is passed for every row of a set, so memoizing on reader
        // identity gives an O(columns) comparison from the second row onward instead. This mirrors
        // MultiEntityMapper<T1,T2>'s ReaderCache, which was added for exactly this reason on the
        // multi-entity path; the single-entity path - the far more common one - never got it.
        //
        // Every hit is re-validated against the reader's current schema and mapping mode, because
        // some providers (Npgsql) recycle a single IDataReader instance across commands on the same
        // pooled physical connection, so reader identity alone can return setters built for a
        // different column layout (the AUD-R9-011 regression).
        Func<string, string>? resolver = JauntyConfig.ColumnNameResolver;

        if (ReaderCache.TryGetValue(reader, out ReaderCacheEntry? entry) && entry.Matches(reader, mode, resolver))
            return entry.Setters;

        var signature = new ReaderSignature(reader, mode, resolver);
        if (!SettersCache.TryGetValue(signature, out PropertySetter<T>[]? setters))
        {
            setters = BuildSetters(reader, mode);
            SettersCache.TryAdd(signature, setters);
        }

        ReaderCache.Remove(reader);
        ReaderCache.Add(reader, new ReaderCacheEntry(reader, mode, resolver, setters));

        return setters;
    }

    private static readonly ConditionalWeakTable<IDataReader, ReaderCacheEntry> ReaderCache = new();

    /// <summary>
    /// Per-reader-instance memoization of the resolved setters, validated against the reader's
    /// current schema on every hit. See the comment in <see cref="GetSetters"/>.
    /// </summary>
    private sealed class ReaderCacheEntry
    {
        private readonly MappingMode _mode;
        private readonly Func<string, string>? _resolver;
        private readonly int _fieldCount;
        private readonly string[] _columnNames;

        public ReaderCacheEntry(IDataReader reader, MappingMode mode, Func<string, string>? resolver, PropertySetter<T>[] setters)
        {
            _mode = mode;
            _resolver = resolver;
            _fieldCount = reader.FieldCount;
            _columnNames = new string[_fieldCount];

            for (int i = 0; i < _fieldCount; i++)
                _columnNames[i] = reader.GetName(i) ?? string.Empty;

            Setters = setters;
        }

        public PropertySetter<T>[] Setters { get; }

        public bool Matches(IDataReader reader, MappingMode mode, Func<string, string>? resolver)
        {
            if (mode != _mode || !ReferenceEquals(resolver, _resolver) || reader.FieldCount != _fieldCount)
                return false;

            for (int i = 0; i < _fieldCount; i++)
            {
                if (!string.Equals(reader.GetName(i), _columnNames[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
    }

    private readonly struct ReaderSignature : IEquatable<ReaderSignature>
    {
        private readonly MappingMode _mode;
        private readonly Func<string, string>? _resolver;
        private readonly string _schemaKey;
        private readonly int _hashCode;

        // AUD-R26: the resolver is part of the key. BuildSetters consults
        // JauntyConfig.ColumnNameResolver to match snake_case columns onto PascalCase properties,
        // so the setters it produces depend on it - but the key used to be the mapping mode and the
        // column names only, which meant registering or changing the resolver after a given shape
        // had been mapped once returned the stale setters forever. Same mutable-process-state
        // capture this file already re-checks per call for DefaultEnumStorage and
        // TypeHandlerRegistry; reference equality is the right comparison because a different
        // delegate instance is a different mapping.
        public ReaderSignature(IDataReader reader, MappingMode mode, Func<string, string>? resolver)
        {
            _mode = mode;
            _resolver = resolver;
            int fieldCount = reader.FieldCount;

            // Build a stable schema key so equality is based on the actual shape,
            // not just the hash code (avoids cache-collision misbinding).
            var parts = new string[fieldCount + 2];
            parts[0] = ((int)mode).ToString();
            parts[1] = fieldCount.ToString();

            for (int i = 0; i < fieldCount; i++)
            {
                parts[i + 2] = reader.GetName(i) ?? string.Empty;
            }

            _schemaKey = string.Join("\u001F", parts);
            _hashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(_schemaKey);
        }

        public bool Equals(ReaderSignature other) => _mode == other._mode
               && ReferenceEquals(_resolver, other._resolver)
               && StringComparer.OrdinalIgnoreCase.Equals(_schemaKey, other._schemaKey);

        public override bool Equals(object? obj) => obj is ReaderSignature other && Equals(other);

        public override int GetHashCode() => _hashCode;
    }

    private static PropertySetter<T>[] BuildSetters(IDataReader reader, MappingMode mode)
    {
        int fieldCount = reader.FieldCount;
        var settersBuffer = new PropertySetter<T>[fieldCount];
        int count = 0;
        var matchedProperties = new bool[Properties.Length];

        // Build a resolver-aware index if ColumnNameResolver is configured.
        // This maps resolver(propertyName) -> property index so that
        // snake_case columns can match PascalCase properties at query time.
        Dictionary<string, int>? resolverIndex = BuildResolverIndex();

        for (int i = 0; i < fieldCount; i++)
        {
            string columnName = reader.GetName(i) ?? throw new InvalidOperationException($"Column {i} has no name");

            if (ColumnToIndex.TryGetValue(columnName, out int propIndex)
                || (resolverIndex != null && resolverIndex.TryGetValue(columnName, out propIndex)))
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("IL", "IL2072:")]
    private static Action<T, IDataRecord, int> CreateSetter(PropertyInfo property)
    {
        Type propertyType = property.PropertyType;
        Type underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        Action<T, IDataRecord, int> fallback = CreateFallbackSetter(property, propertyType, underlyingType);

        // TypeHandlerRegistry is mutable process-wide state (RegisterTypeHandler/RemoveTypeHandler
        // can be called at any time, e.g. multi-tenant apps swapping handlers per request, or tests).
        // This setter is compiled once inside T's static constructor and reused for the lifetime of
        // the process, so the handler must be re-resolved on every call rather than captured once -
        // otherwise a handler registered/changed after T's first read would silently never apply.
        return (target, record, index) =>
        {
            if (TypeHandlerRegistry.HasHandlers && TypeHandlerRegistry.TryGetHandler(underlyingType, out ITypeHandler? handler) && handler is not null)
            {
                object dbValue = record.GetValue(index);

                try
                {
                    object? convertedValue = handler.Parse(dbValue);

                    // Convert to the property type (handles nullable)
                    if (propertyType != underlyingType && convertedValue is not null)
                    {
                        property.SetValue(target, DbValueConverter.ChangeType(convertedValue, underlyingType));
                    }
                    else
                    {
                        property.SetValue(target, convertedValue);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Type handler '{handler.GetType().Name}' failed to parse value for property '{property.Name}' on type '{typeof(T).Name}'.", ex);
                }

                return;
            }

            fallback(target, record, index);
        };
    }

    private static Action<T, IDataRecord, int> CreateFallbackSetter(PropertyInfo property, Type propertyType, Type underlyingType)
    {
        if (underlyingType.IsEnum)
        {
            EnumStorageAttribute? enumAttr = property.GetCustomAttribute<EnumStorageAttribute>();

            // An attribute is genuinely immutable, so its choice can be baked into the setter.
            if (enumAttr is not null)
            {
                return enumAttr.Storage == EnumStorage.String
                    ? CreateStringEnumSetter(property, underlyingType)
                    : CreateConvertingSetter(property, propertyType);
            }

            // AUD-R25: without an attribute the storage comes from JauntyConfig.DefaultEnumStorage,
            // which is mutable process-wide state - callers can change it at runtime, e.g. tests or
            // multi-tenant apps - so it must be re-checked on every call rather than captured once.
            // This used to evaluate "enumAttr?.Storage ?? JauntyConfig.DefaultEnumStorage" here, in
            // MetadataCache<T>'s static constructor, baking the answer in for the process lifetime,
            // while the write path's BuildValueConverter deliberately deferred the same lookup into
            // its per-call closure - and cited CreateSetter as the precedent for doing so, which
            // CreateSetter did not actually follow.
            //
            // The consequence was that an application setting DefaultEnumStorage = String after
            // entity T had been read once wrote enum columns as strings while continuing to read
            // them as numerics: Enum.Parse on the stored name fell into the numeric-fallback catch
            // and threw InvalidOperationException per row, or silently misbound.
            // JauntyConfig.Reset() resets _defaultEnumStorage to Numeric but cannot reset
            // MetadataCache<T>'s static state, which made this reachable in exactly the test
            // scenario the write path's comment worries about.
            Action<T, IDataRecord, int> stringSetter = CreateStringEnumSetter(property, underlyingType);
            Action<T, IDataRecord, int> numericSetter = CreateConvertingSetter(property, propertyType);

            return (target, record, index) =>
            {
                if (JauntyConfig.DefaultEnumStorage == EnumStorage.String)
                    stringSetter(target, record, index);
                else
                    numericSetter(target, record, index);
            };
        }

        return CreateConvertingSetter(property, propertyType);
    }

    /// <summary>Reads an enum stored as its name, falling back to a numeric representation.</summary>
    private static Action<T, IDataRecord, int> CreateStringEnumSetter(PropertyInfo property, Type underlyingType)
    {
        return (target, record, index) =>
        {
            object dbValue = record.GetValue(index);
            string strValue = dbValue.ToString() ?? string.Empty;
            object? convertedValue;

            try
            {
                // Try Enum.Parse case-insensitive
                convertedValue = Enum.Parse(underlyingType, strValue, ignoreCase: true);
            }
            catch
            {
                // If it's already numeric, try parsing as that
                try
                {
                    var numValue = Convert.ChangeType(dbValue, Enum.GetUnderlyingType(underlyingType), CultureInfo.InvariantCulture);
                    convertedValue = Enum.ToObject(underlyingType, numValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Cannot convert value '{strValue}' to enum type '{underlyingType.Name}' for property '{property.Name}' on type '{typeof(T).Name}'.", ex);
                }
            }

            // convertedValue is already boxed as underlyingType (the enum type); reflection's
            // SetValue handles boxing it into a Nullable<TEnum> property without further conversion.
            property.SetValue(target, convertedValue);
        };
    }

    /// <summary>The default path: Convert.ChangeType via a compiled expression tree.</summary>
    private static Action<T, IDataRecord, int> CreateConvertingSetter(PropertyInfo property, Type propertyType)
    {
        ParameterExpression target = Expression.Parameter(typeof(T), "target");
        ParameterExpression record = Expression.Parameter(typeof(IDataRecord), "record");
        ParameterExpression index = Expression.Parameter(typeof(int), "index");
        MethodCallExpression getValue = Expression.Call(record, typeof(IDataRecord).GetMethod(nameof(IDataRecord.GetValue))!, index);

        Type conversionType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        Expression valueExpression = Expression.Convert(
            Expression.Call(typeof(DbValueConverter).GetMethod(nameof(DbValueConverter.ChangeType), [typeof(object), typeof(Type)])!,
            getValue, Expression.Constant(conversionType)), conversionType);

        if (propertyType != conversionType)
            valueExpression = Expression.Convert(valueExpression, propertyType);

        BinaryExpression assign = Expression.Assign(Expression.Property(target, property), valueExpression);
        return Expression.Lambda<Action<T, IDataRecord, int>>(assign, target, record, index).Compile();
    }

    private static Func<T, object?> CreateGetter(PropertyInfo property)
    {
        ParameterExpression target = Expression.Parameter(typeof(T), "target");
        MemberExpression access = Expression.Property(target, property);
        UnaryExpression box = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<T, object?>>(box, target).Compile();
    }

    private static Dictionary<string, int>? BuildResolverIndex()
    {
        Func<string, string>? resolver = JauntyConfig.ColumnNameResolver;
        if (resolver == null) return null;

        var index = new Dictionary<string, int>(Properties.Length, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < Properties.Length; i++)
        {
            string resolved = resolver(Properties[i].Property.Name);
            if (!string.IsNullOrEmpty(resolved))
                index[resolved] = i;
        }

        return index;
    }

    private static bool IsNonNullableType(Type type) => type.IsValueType && Nullable.GetUnderlyingType(type) is null;
}

/// <summary>
/// Represents metadata context for a property during entity mapping.
/// </summary>
/// <typeparam name="T">The entity type containing the property.</typeparam>
public readonly struct PropertyContext<T>
{
    /// <summary>
    /// Gets the property information.
    /// </summary>
    public PropertyInfo Property { get; }

    /// <summary>
    /// Gets the setter action for standard data readers.
    /// </summary>
    public Action<T, IDataRecord, int> Setter { get; }

    /// <summary>
    /// Gets the getter function for retrieving property values.
    /// </summary>
    public Func<T, object?> Getter { get; }

    /// <summary>
    /// Gets the name of the property.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the name of the corresponding database column.
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// Gets a value indicating whether the property is a non-nullable value type.
    /// </summary>
    public bool IsNonNullable { get; }

    /// <summary>
    /// Initializes a new instance of the  class.
    /// </summary>
    /// The  for the property this context describes.
    /// A delegate that sets the property value from an  using a column ordinal.
    /// A delegate that gets the property value from the target instance.
    /// The CLR property name.
    /// The database column name mapped to the property.
    /// True if the property is non-nullable; false if the property accepts nulls.
    public PropertyContext(PropertyInfo property, Action<T, IDataRecord, int> setter, Func<T, object?> getter, string propertyName, string columnName, bool isNonNullable)
    {
        Property = property;
        Setter = setter;
        Getter = getter;
        PropertyName = propertyName;
        ColumnName = columnName;
        IsNonNullable = isNonNullable;
    }
}

/// <summary>
/// Represents a property setter with its associated column ordinal for entity mapping.
/// </summary>
/// <typeparam name="T">The entity type containing the property.</typeparam>
public readonly struct PropertySetter<T>
{
    /// 
    /// Initializes a new instance of the  struct.
    /// 
    public PropertySetter(PropertyContext<T> context, int ordinal)
    {
        Context = context;
        Ordinal = ordinal;
    }

    /// <summary>
    /// Gets the <see cref="PropertyContext{T}"/> containing metadata and the setter delegate for the property.
    /// </summary>
    public PropertyContext<T> Context { get; }

    /// <summary>
    /// Gets the zero-based ordinal position of the column in the data reader.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>
    /// Sets the property value on the target entity from the data record.
    /// </summary>
    /// <param name="target">The target entity to set the property on.</param>
    /// <param name="record">The data record containing the value.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a NULL value is encountered for a non-nullable property.
    /// </exception>
    public void Set(T target, IDataRecord record)
    {
        if (!record.IsDBNull(Ordinal))
            Context.Setter(target, record, Ordinal);
        else if (Context.IsNonNullable)
            throw new InvalidOperationException($"Cannot assign NULL to non-nullable property '{Context.PropertyName}'.");
    }
}