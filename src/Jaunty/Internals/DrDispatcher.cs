using System.Data;
using System.Dynamic;

using Jaunty.Core;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;

namespace Jaunty.Internals;

internal static class DrDispatcher
{
    internal static Func<IDataReader, T> Resolve<T>(IDataReader reader, CommandOptions<T> options, MappingMode mode) where T : new()
    {
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

    private static Func<IDataReader, T>? TryResolveSpecialType<T>(IDataReader reader) where T : new()
    {
        var type = typeof(T);

        // Dictionary<string, object> or Dictionary<string, TValue>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];

            if (keyType != typeof(string))
                throw new NotSupportedException($"Dictionary key type must be string, got {keyType.Name}");

            return CreateDictionaryMapper<T>(reader, valueType);
        }

        // dynamic (object at compile time) - return ExpandoObject
        if (type == typeof(object))
        {
            return CreateExpandoMapper<T>(reader);
        }

        return null;
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

    private static Func<IDataReader, T> CreateDictionaryMapper<T>(IDataReader reader, Type valueType) where T : new()
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
            return r =>
            {
                var dict = new Dictionary<string, object?>(fieldCount, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < fieldCount; i++)
                {
                    object? value;
                    if (r.IsDBNull(i))
                    {
                        value = null;
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
                return (T)(object)dict;
            };
        }
    }
}
