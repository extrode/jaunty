using System.Data;
using System.Data.Common;

using Jaunty.Configuration;
using Jaunty.Core;

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
        // Prefer the per-result-set factory: shape validated once here instead of per row.
        if (mode == MappingMode.Strict && MappedCache<T>.MapperFactory is not null)
            return MappedCache<T>.MapperFactory(reader);
        if (mode == MappingMode.Strict && MappedCache<T>.Mapper is not null)
            return MappedCache<T>.Mapper;

        // 3. Special Types (Dictionary, dynamic - uses extension hook)
        Func<IDataReader, T>? specialMapper = TryResolveSpecialTypeFromExtension<T>(reader);
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
        if (JauntyConfig.SpecialTypeMapperResolver?.Invoke(typeof(T), reader) is Func<IDataReader, object?> extensionMapper)
        {
            return reader => (T)(extensionMapper(reader) ?? throw new InvalidOperationException("SpecialTypeMapperResolver returned null"));
        }
        return null;
    }

    // Func<in T, out TResult> is contravariant in its argument, so a Func<IDataReader, T> is a
    // Func<DbDataReader, T> as it stands. Each arm below used to wrap the delegate in a lambda, which
    // cost a closure per query and a second delegate call per row - the entire pipeline overhead
    // left on the custom-mapper path once the mapper itself is the caller's.
    internal static Func<DbDataReader, T> Resolve<T>(DbDataReader reader, CommandOptions<T> options, MappingMode mode) where T : new()
    {
        // 1. User override
        if (options.Mapper is not null)
            return options.Mapper;

        // 2. IMapped<T> implementation (Source Generated)
        // Only safe for strict/full-shape mapping. Projection/partial queries may omit columns.
        // Prefer the per-result-set factory: shape validated once here instead of per row.
        if (mode == MappingMode.Strict && MappedCache<T>.MapperFactory is not null)
            return MappedCache<T>.MapperFactory(reader);
        if (mode == MappingMode.Strict && MappedCache<T>.Mapper is not null)
            return MappedCache<T>.Mapper;

        // 3. Special Types
        Func<IDataReader, T>? specialMapper = TryResolveSpecialTypeFromExtension<T>(reader);
        if (specialMapper is not null)
            return specialMapper;

        // 4. Fallback to Reflection Extension (if loaded)
        if (JauntyConfig.ReflectionMapperResolver is not null)
        {
            var resolved = JauntyConfig.ReflectionMapperResolver(typeof(T), mode);

            if (resolved is Func<DbDataReader, T> dbMapper)
                return dbMapper;

            if (resolved is Func<IDataReader, T> dataReaderMapper)
                return dataReaderMapper;
        }

        // 5. Fail
        throw new InvalidOperationException(
            $"No mapper found for type '{typeof(T).Name}'. " +
            "Ensure the class is marked with [Table] for source generation, " +
            "provide a manual mapper in CommandOptions, " +
            "or add the 'Jaunty.Extensions.Reflection' package for runtime mapping.");
    }
}