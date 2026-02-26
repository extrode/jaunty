using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;

using Jaunty.Configuration;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Provides reflection-based mapping for special types (Dictionary, KeyValuePair, ValueTuple, ExpandoObject).
/// These types cannot be source-generated and require runtime reflection.
/// </summary>
/// <remarks>
/// <para>
/// This class is not NativeAOT-safe. For NativeAOT applications, consider:
/// </para>
/// <list type="bullet">
/// <item><description>Using strongly-typed entities instead of Dictionary</description></item>
/// <item><description>Using anonymous types with QueryPartial for projections</description></item>
/// <item><description>Excluding this assembly from trimming if special type mapping is required</description></item>
/// </list>
/// </remarks>
public static class SpecialTypeMappers
{
    /// <summary>
    /// Registers special type mappers with Jaunty configuration.
    /// Call this at application startup to enable Dictionary, KeyValuePair, ValueTuple, and ExpandoObject mapping.
    /// </summary>
    public static void Register()
    {
        JauntyConfig.SpecialTypeMapperResolver = ResolveSpecialTypeMapper;
    }

    private static object? ResolveSpecialTypeMapper(Type type, IDataReader reader)
    {
        // Dictionary<string, object> or Dictionary<string, TValue>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            Type keyType = type.GetGenericArguments()[0];
            Type valueType = type.GetGenericArguments()[1];

            return keyType == typeof(string)
                ? CreateDictionaryMapper(type, reader, valueType)
                : throw new NotSupportedException($"Dictionary key type must be string, got {keyType.Name}");
        }

        // KeyValuePair<TKey, TValue> - two columns: first is Key, second is Value
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            return reader.FieldCount >= 2
                ? CreateKeyValuePairMapper(type, reader, type.GetGenericArguments())
                : throw new InvalidOperationException(
                    $"Type '{type.Name}' requires at least 2 columns, but query returned {reader.FieldCount}.");
        }

        // ValueTuple - positional mapping
        if (type.IsValueType && type.FullName?.StartsWith("System.ValueTuple`") == true)
        {
            var typeArgs = type.GetGenericArguments();
            return reader.FieldCount >= typeArgs.Length
                ? CreateValueTupleMapper(type, reader, typeArgs)
                : throw new InvalidOperationException(
                    $"Type 'ValueTuple<{string.Join(", ", typeArgs.Select(t => t.Name))}>' requires {typeArgs.Length} columns, but query returned {reader.FieldCount}.");
        }

        // dynamic (object at compile time) - return ExpandoObject
        return type == typeof(object) ? CreateExpandoMapper(reader) : null;
    }

    private static object CreateKeyValuePairMapper(Type type, IDataReader reader, Type[] typeArgs)
    {
        var keyType = typeArgs[0];
        var valueType = typeArgs[1];

        return new Func<IDataReader, object>(r =>
        {
            var key = r.IsDBNull(0) ? GetDefault(keyType) : ConvertValue(r.GetValue(0), keyType);
            var value = r.IsDBNull(1) ? GetDefault(valueType) : ConvertValue(r.GetValue(1), valueType);

            // Create KeyValuePair using reflection (it's a struct)
            var kvp = Activator.CreateInstance(type, key, value);
            return kvp!;
        });
    }

    private static object CreateValueTupleMapper(Type type, IDataReader reader, Type[] typeArgs)
    {
        var itemCount = typeArgs.Length;

        return new Func<IDataReader, object>(r =>
        {
            var values = new object?[itemCount];
            for (int i = 0; i < itemCount; i++)
            {
                values[i] = r.IsDBNull(i) ? GetDefault(typeArgs[i]) : ConvertValue(r.GetValue(i), typeArgs[i]);
            }

            // Create ValueTuple using Activator
            var tuple = Activator.CreateInstance(type, values);
            return tuple!;
        });
    }

    private static object? GetDefault(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
            return GetDefault(targetType);

        var valueType = value.GetType();
        if (targetType.IsAssignableFrom(valueType))
            return value;

        // Handle nullable types
        var underlyingType = System.Nullable.GetUnderlyingType(targetType) ?? targetType;

        return Convert.ChangeType(value, underlyingType);
    }

    private static object CreateExpandoMapper(IDataReader reader)
    {
        // Cache column names
        var fieldCount = reader.FieldCount;
        var columnNames = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++)
            columnNames[i] = reader.GetName(i);

        return new Func<IDataReader, object>(r =>
        {
            IDictionary<string, object?> expando = new ExpandoObject();
            for (int i = 0; i < fieldCount; i++)
            {
                var value = r.IsDBNull(i) ? null : r.GetValue(i);
                expando[columnNames[i]] = value;
            }
            return (object)expando;
        });
    }

    private static object CreateDictionaryMapper(Type type, IDataReader reader, Type valueType)
    {
        // Cache column names
        var fieldCount = reader.FieldCount;
        var columnNames = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++)
            columnNames[i] = reader.GetName(i);

        if (valueType == typeof(object))
        {
            // Dictionary<string, object> - store values as-is
            return new Func<IDataReader, object>(r =>
            {
                var dict = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    var value = r.IsDBNull(i) ? null : r.GetValue(i);
                    dict[columnNames[i]] = value;
                }
                return (object)dict;
            });
        }
        else
        {
            // Dictionary<string, TValue> - convert values to TValue
            // We need to create the properly typed dictionary using reflection
            var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);

            return new Func<IDataReader, object>(r =>
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
                return (object)dict;
            });
        }
    }
}
