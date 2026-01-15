using System.Data;

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

        // 2. IMapped<T> implementation (cached)
        if (MappedCache<T>.Mapper is not null)
            return MappedCache<T>.Mapper;

        // 3. Metadata reflection fallback
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
}
