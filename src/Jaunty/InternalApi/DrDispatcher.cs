using System.Data;

using Jaunty.InternalApi.Entity;
using Jaunty.InternalApi.Enums;
using Jaunty.PublicApi;

namespace Jaunty.InternalApi;

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