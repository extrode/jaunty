using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;

using Jaunty.Configuration;
using Jaunty.Internals.Read;

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
    /// <remarks>
    /// Idempotent: if <see cref="JauntyConfig.SpecialTypeMapperResolver"/> is already set (by a
    /// prior call to this method, or by a custom resolver configured directly), this call is a
    /// silent no-op rather than overwriting it.
    /// </remarks>
    public static void Register()
    {
        JauntyConfig.SpecialTypeMapperResolver ??= ResolveSpecialTypeMapper;
    }

    private static object ResolveSpecialTypeMapper(Type type, IDataReader reader)
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
            Type[] typeArgs = type.GetGenericArguments();

            // ValueTuple`8's 8th type argument is always the nested "Rest" tuple (C# tuple
            // syntax only ever nests beyond 7 elements), which this positional mapper treats
            // as a single plain column instead of flattening - silently truncating/misbinding
            // rather than mapping correctly. Fail fast instead.
            if (typeArgs.Length >= 8)
                throw new NotSupportedException(
                    $"ValueTuple types with 8 or more elements (nested 'Rest' tuples) are not supported for query mapping; got '{type.Name}'.");

            return reader.FieldCount >= typeArgs.Length
                ? CreateValueTupleMapper(type, reader, typeArgs)
                : throw new InvalidOperationException(
                    $"Type 'ValueTuple<{string.Join(", ", typeArgs.Select(t => t.Name))}>' requires {typeArgs.Length} columns, but query returned {reader.FieldCount}.");
        }

        // dynamic (object at compile time) - return ExpandoObject
        return type == typeof(object) ? CreateExpandoMapper(reader) : null!;
    }

    private static object CreateKeyValuePairMapper(Type type, IDataReader reader, Type[] typeArgs)
    {
        Type keyType = typeArgs[0];
        Type valueType = typeArgs[1];

        return new Func<IDataReader, object>(r =>
        {
            object? key = r.IsDBNull(0) ? GetDefault(keyType, "Key") : ConvertValue(r.GetValue(0), keyType);
            object? value = r.IsDBNull(1) ? GetDefault(valueType, "Value") : ConvertValue(r.GetValue(1), valueType);
            // Create KeyValuePair using reflection (it's a struct)
            object? kvp = Activator.CreateInstance(type, key, value);

            return kvp!;
        });
    }

    private static object CreateValueTupleMapper(Type type, IDataReader reader, Type[] typeArgs)
    {
        int itemCount = typeArgs.Length;

        return new Func<IDataReader, object>(r =>
        {
            object?[] values = new object?[itemCount];

            for (int i = 0; i < itemCount; i++)
                values[i] = r.IsDBNull(i) ? GetDefault(typeArgs[i], $"Item{i + 1}") : ConvertValue(r.GetValue(i), typeArgs[i]);

            // Create ValueTuple using Activator
            object? tuple = Activator.CreateInstance(type, values);
            return tuple!;
        });
    }

    /// <summary>
    /// Returns the default value for a NULL column, matching the entity-mapping path
    /// (<c>PropertySetter&lt;T&gt;.Set</c>) which throws for NULL into a non-nullable value type
    /// instead of silently coercing it to <c>default(T)</c>.
    /// </summary>
    private static object? GetDefault(Type type, string elementName = "value")
    {
        if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
            throw new InvalidOperationException(
                $"Cannot assign NULL to non-nullable element '{elementName}' of type '{type.Name}'.");

        return null;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
            return GetDefault(targetType);

        Type valueType = value.GetType();
        if (targetType.IsAssignableFrom(valueType))
            return value;

        // Handle nullable types
        Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return DbValueConverter.ChangeType(value, underlyingType);
    }

    private static object CreateExpandoMapper(IDataReader reader)
    {
        // Cache column names
        int fieldCount = reader.FieldCount;
        string[] columnNames = new string[fieldCount];

        for (int i = 0; i < fieldCount; i++)
            columnNames[i] = reader.GetName(i);

        // AUD-R34-024: CreateDictionaryMapper below has done this since AUD-R26, which named both
        // untyped-row sites so they could not drift again - this one was never carried over. An
        // ExpandoObject is an indexer over a dictionary too, so a Query<dynamic> over a join with
        // two Id columns silently kept only the last and dropped the first.
        columnNames = DuplicateColumnNames.Disambiguate(columnNames);

        // AUD-R35-226: an ExpandoObject's IDictionary<string, object?> is ordinal case-*sensitive*
        // and its comparer is not configurable, while CreateDictionaryMapper below builds its
        // dictionaries with StringComparer.OrdinalIgnoreCase. So Query<Dictionary<string, object>>()
        // ["customerid"] resolves against a CustomerID column and ((IDictionary<string, object?>)row)
        // ["customerid"] on a Query<dynamic> row does not. This is not fixable in the mapper: the
        // dynamic member access these rows exist for is case-sensitive anyway (C# is), so the only
        // way to align the two would be to stop returning an ExpandoObject for dynamic, which is the
        // documented contract. Recorded here so the pair is not read as an oversight - AUD-R34-024
        // aligned their duplicate-column handling, and this is the half that cannot be aligned.
        return new Func<IDataReader, object>(r =>
        {
            IDictionary<string, object?> expando = new ExpandoObject();

            for (int i = 0; i < fieldCount; i++)
            {
                object? value = r.IsDBNull(i) ? null : r.GetValue(i);
                expando[columnNames[i]] = value;
            }

            return expando;
        });
    }

    private static object CreateDictionaryMapper(Type type, IDataReader reader, Type valueType)
    {
        int fieldCount = reader.FieldCount;
        string[] columnNames = new string[fieldCount];

        for (int i = 0; i < fieldCount; i++)
            columnNames[i] = reader.GetName(i);

        // AUD-R26: the same indexer collapse QueryPartialList had. The finding named both sites and
        // required that any fix cover both "so they cannot drift again", so the rule is shared rather
        // than copied - this is the sibling untyped-row path behind Query<Dictionary<string, object>>.
        columnNames = DuplicateColumnNames.Disambiguate(columnNames);

        if (valueType == typeof(object))
        {
            // Dictionary<string, object> - store values as-is
            return new Func<IDataReader, object>(r =>
            {
                var dict = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    object? value = r.IsDBNull(i) ? null : r.GetValue(i);
                    dict[columnNames[i]] = value;
                }
                return dict;
            });
        }
        else
        {
            // Dictionary<string, TValue> - convert values to TValue
            // We need to create the properly typed dictionary using reflection
            Type dictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);

            return new Func<IDataReader, object>(r =>
            {
                // Create Dictionary<string, TValue> with case-insensitive comparer
                var dict = (IDictionary)Activator.CreateInstance(dictType, fieldCount, StringComparer.OrdinalIgnoreCase)!;

                for (int i = 0; i < fieldCount; i++)
                {
                    object? value = r.IsDBNull(i)
                        ? GetDefault(valueType, columnNames[i])
                        : ConvertValue(r.GetValue(i), valueType);
                    dict[columnNames[i]] = value;
                }

                return dict;
            });
        }
    }
}