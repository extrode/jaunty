using System.Data;

using Jaunty.Entity;
using Jaunty.Enums;

namespace Jaunty;

internal static class DrDispatcher
{
    internal static Func<IDataReader, T> Resolve<T>(IDataReader reader, Func<IDataReader, T>? userMapper, MappingMode mode) where T : new()
    {
        // 1. User override
        if (userMapper is not null)
            return userMapper;

        // 2. IMapped<T> implementation (cached)
        if (MappedCache<T>.Mapper is not null)
            return MappedCache<T>.Mapper;

        // 3. Metadata reflection fallback
        var setters = MetadataCache<T>.GetSetters(reader, mode);
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