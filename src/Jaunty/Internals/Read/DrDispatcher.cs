using System.Data;
using System.Data.Common;

using Jaunty.Core;
using Jaunty.Internals.Enums;

using Jaunty.Configuration;

namespace Jaunty.Internals.Read;

internal static class DrDispatcher
{
    internal static Func<IDataReader, T> Resolve<T>(IDataReader reader, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        // 1. User override (Zero Reflection)
        if (options.Mapper is not null)
            return options.Mapper;

        // 2. IMapped<T> implementation (Source Generated - Zero Reflection)
        // Only safe for strict/full-shape mapping. Projection/partial queries may omit columns.
        if (mode == MappingMode.Strict && MappedCache<T>.Mapper is not null)
            return MappedCache<T>.Mapper;

        // 3. Special Types (Dictionary, dynamic - uses extension hook)
        var specialMapper = TryResolveSpecialTypeFromExtension<T>(reader);
        if (specialMapper is not null)
            return specialMapper;

        // 4. Fallback to Reflection Extension (if loaded)
        if (JauntyConfig.ReflectionMapperResolver?.Invoke(typeof(T), mode) is Func<IDataReader, T> reflectionMapper)
            return reflectionMapper;

        // 5. Fail - No mapper available
        throw new InvalidOperationException(
            $"No mapper found for type '{typeof(T).Name}'. " +
            "Ensure the class is marked with [Table] for source generation, " +
            "provide a manual mapper in CommandOptions, " +
            "or add the 'Jaunty.Extensions.Reflection' package for runtime mapping.");
    }

    private static Func<IDataReader, T>? TryResolveSpecialTypeFromExtension<T>(IDataReader reader) where T : new()
    {
        if (JauntyConfig.SpecialTypeMapperResolver?.Invoke(typeof(T), reader) is Func<IDataReader, object> extensionMapper)
        {
            return reader => (T)extensionMapper(reader);
        }
        return null;
    }

    internal static Func<DbDataReader, T> Resolve<T>(DbDataReader reader, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        // 1. User override
        if (options.Mapper is not null)
            return dbReader => options.Mapper(dbReader);

        // 2. IMapped<T> implementation (Source Generated)
        // Only safe for strict/full-shape mapping. Projection/partial queries may omit columns.
        if (mode == MappingMode.Strict && MappedCache<T>.Mapper is not null)
            return dbReader => MappedCache<T>.Mapper(dbReader);

        // 3. Special Types
        var specialMapper = TryResolveSpecialTypeFromExtension<T>(reader);
        if (specialMapper is not null)
            return dbReader => specialMapper(dbReader);

        // 4. Fallback to Reflection Extension (if loaded)
        if (JauntyConfig.ReflectionMapperResolver != null)
        {
            var resolved = JauntyConfig.ReflectionMapperResolver(typeof(T), mode);
            if (resolved is Func<DbDataReader, T> dbMapper)
                return dbMapper;
            if (resolved is Func<IDataReader, T> dataReaderMapper)
                return dbReader => dataReaderMapper(dbReader);
        }

        // 5. Fail
        throw new InvalidOperationException(
            $"No mapper found for type '{typeof(T).Name}'. " +
            "Ensure the class is marked with [Table] for source generation, " +
            "provide a manual mapper in CommandOptions, " +
            "or add the 'Jaunty.Extensions.Reflection' package for runtime mapping.");
    }
}
