using System.Collections;
using System.Data;
using System.Data.Common;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Dynamic;

using Jaunty.Core;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;

namespace Jaunty.Internals.Read;

internal static class DrDispatcher
{
    internal static Func<IDataReader, T> Resolve<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicMethods)] 
#endif
        T>(IDataReader reader, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        if (reader is DbDataReader dbDataReader)
        {
            var fastMap = Resolve(dbDataReader, options, mode);
            return r => fastMap((DbDataReader)r);
        }

        // 1. User override
        if (options.Mapper is not null)
            return options.Mapper;

        // 2. Check for special types (Dictionary, dynamic)
        var specialMapper = TryResolveSpecialType<T>(reader);
        if (specialMapper is not null)
            return specialMapper;

        // 3. IMapped<T> implementation (cached)
        if (MappedCache<T>.Mapper is not null)
            return MappedCache<T>.Mapper;

        // 4. Metadata reflection fallback
        PropertySetter<T>[] setters;
        try
        {
            setters = MetadataCache<T>.GetSetters(reader, mode);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to get column name") && reader.GetType().Name.Contains("SQLite"))
        {
            // Handle SQLite async DataReader issue by providing a helpful error message
            throw new InvalidOperationException($"SQLite async DataReader issue: {ex.Message}. This may be a limitation of SQLite's async DataReader implementation. Consider using synchronous methods or ensuring the reader state is valid.", ex);
        }

        return reader =>
        {
            var entity = new T();
#if NET8_0_OR_GREATER
            ReadOnlySpan<PropertySetter<T>> localSetters = setters;
            foreach (ref readonly var setter in localSetters)
                setter.Set(entity, reader);
#else
            for (int i = 0; i < setters.Length; i++)
                setters[i].Set(entity, reader);
#endif
            return entity;
        };
    }

    internal static Func<DbDataReader, T> Resolve<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicMethods)] 
#endif
        T>(DbDataReader reader, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        // 1. User override
        if (options.Mapper is not null)
            return dbReader => options.Mapper(dbReader);

        // 2. Check for special types (Dictionary, dynamic)
        var specialMapper = TryResolveSpecialType<T>(reader);
        if (specialMapper is not null)
            return dbReader => specialMapper(dbReader);

        // 3. IMapped<T> implementation (cached)
        if (MappedCache<T>.Mapper is not null)
            return dbReader => MappedCache<T>.Mapper(dbReader);

        // 4. Metadata reflection fallback
        PropertySetter<T>[] setters;
        try
        {
            setters = MetadataCache<T>.GetSetters(reader, mode);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Failed to get column name") && reader.GetType().Name.Contains("SQLite"))
        {
            throw new InvalidOperationException($"SQLite async DataReader issue: {ex.Message}. This may be a limitation of SQLite's async DataReader implementation. Consider using synchronous methods or ensuring the reader state is valid.", ex);
        }

        return reader =>
        {
            var entity = new T();
#if NET8_0_OR_GREATER
            ReadOnlySpan<PropertySetter<T>> localSetters = setters;
            foreach (ref readonly var setter in localSetters)
                setter.SetFast(entity, reader);
#else
            for (int i = 0; i < setters.Length; i++)
                setters[i].SetFast(entity, reader);
#endif
            return entity;
        };
    }

    private static Func<IDataReader, T>? TryResolveSpecialType<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        T>(IDataReader reader) where T : new()
    {
        var type = typeof(T);

        // Dictionary<string, object> or Dictionary<string, TValue>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];

            return keyType != typeof(string)
                ? throw new NotSupportedException($"Dictionary key type must be string, got {keyType.Name}")
                : CreateDictionaryMapper<T>(reader, valueType);
        }

        // KeyValuePair<TKey, TValue> - two columns: first is Key, second is Value
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            return reader.FieldCount < 2
                ? throw new InvalidOperationException(
                    $"Type '{type.Name}' requires at least 2 columns, but query returned {reader.FieldCount}.")
                : CreateKeyValuePairMapper<T>(reader, type.GetGenericArguments());
        }

        // ValueTuple - positional mapping
        if (type.IsValueType && type.FullName?.StartsWith("System.ValueTuple`") == true)
        {
            var typeArgs = type.GetGenericArguments();
            if (reader.FieldCount < typeArgs.Length)
                throw new InvalidOperationException(
                    $"Type 'ValueTuple<{string.Join(", ", typeArgs.Select(t => t.Name))}>' requires {typeArgs.Length} columns, but query returned {reader.FieldCount}.");

            return CreateValueTupleMapper<T>(reader, typeArgs);
        }

        // dynamic (object at compile time) - return ExpandoObject
        return type == typeof(object) ? CreateExpandoMapper<T>(reader) : null;
    }

    private static Func<IDataReader, T> CreateKeyValuePairMapper<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] 
#endif
        T>(IDataReader reader, Type[] typeArgs) where T : new()
    {
        var keyType = typeArgs[0];
        var valueType = typeArgs[1];

        return r =>
        {
            var key = r.IsDBNull(0) ? GetDefault(keyType) : ConvertValue(r.GetValue(0), keyType);
            var value = r.IsDBNull(1) ? GetDefault(valueType) : ConvertValue(r.GetValue(1), valueType);

            // Create KeyValuePair using reflection (it's a struct)
            var kvp = Activator.CreateInstance(typeof(T), key, value);
            return (T)kvp!;
        };
    }

    private static Func<IDataReader, T> CreateValueTupleMapper<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] 
#endif
        T>(IDataReader reader, Type[] typeArgs) where T : new()
    {
        var itemCount = typeArgs.Length;

        return r =>
        {
            var values = new object?[itemCount];
            for (int i = 0; i < itemCount; i++)
            {
                values[i] = r.IsDBNull(i) ? GetDefault(typeArgs[i]) : ConvertValue(r.GetValue(i), typeArgs[i]);
            }

            // Create ValueTuple using Activator
            var tuple = Activator.CreateInstance(typeof(T), values);
            return (T)tuple!;
        };
    }

    private static object? GetDefault(
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static object? ConvertValue(object value, 
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        Type targetType)
    {
        if (value is null)
            return GetDefault(targetType);

        var valueType = value.GetType();
        if (targetType.IsAssignableFrom(valueType))
            return value;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return Convert.ChangeType(value, underlyingType);
    }

    private static Func<IDataReader, T> CreateExpandoMapper<T>(IDataReader reader) where T : new()
    {
        // Cache column names
        var fieldCount = reader.FieldCount;
        var columnNames = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++)
            columnNames[i] = reader.GetName(i);

        return r =>
        {
            IDictionary<string, object?> expando = new ExpandoObject();
            for (int i = 0; i < fieldCount; i++)
            {
                var value = r.IsDBNull(i) ? null : r.GetValue(i);
                expando[columnNames[i]] = value;
            }
            return (T)(object)expando;
        };
    }

#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("Reflection-based dictionary mapping is not AOT-safe.")]
#endif
    private static Func<IDataReader, T> CreateDictionaryMapper<T>(IDataReader reader, 
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        Type valueType) where T : new()
    {
        // Cache column names and ordinals
        var fieldCount = reader.FieldCount;
        var columnNames = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++)
            columnNames[i] = reader.GetName(i);

        if (valueType == typeof(object))
        {
            // Dictionary<string, object> - store values as-is
            return r =>
            {
                var dict = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    var value = r.IsDBNull(i) ? null : r.GetValue(i);
                    dict[columnNames[i]] = value;
                }
                return (T)(object)dict;
            };
        }
        else
        {
            // Dictionary<string, TValue> - convert values to TValue
            // We need to create the properly typed dictionary using reflection
            var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
            var addMethod = dictType.GetMethod("Add")!;

            return r =>
            {
                // Create Dictionary<string, TValue> with case-insensitive comparer
                var dict = (IDictionary)Activator.CreateInstance(
                    dictType,
                    fieldCount,
                    StringComparer.OrdinalIgnoreCase)!;

                for (int i = 0; i < fieldCount; i++)
                {
                    object? value;
                    if (r.IsDBNull(i))
                    {
                        value = valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
                    }
                    else
                    {
                        var raw = r.GetValue(i);
                        value = valueType.IsAssignableFrom(raw.GetType())
                            ? raw
                            : Convert.ChangeType(raw, valueType);
                    }
                    dict[columnNames[i]] = value;
                }
                return (T)dict;
            };
        }
    }
}
