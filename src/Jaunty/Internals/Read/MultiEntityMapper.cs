using System.Collections.Concurrent;
using System.Data;

using Jaunty.Configuration;

namespace Jaunty.Internals.Read;

/// <summary>
/// Hook for multi-entity mapping. Actual implementation resides in extensions or source-gen.
/// </summary>
internal sealed class MultiEntityMapper<T1, T2> where T1 : new() where T2 : new()
{
    private static readonly ConcurrentDictionary<(Type, Type), MultiEntityMapper<T1, T2>> _cache = new();

    private readonly Action<T1, IDataRecord> _applyT1;
    private readonly Action<T2, IDataRecord> _applyT2;

    private MultiEntityMapper(Action<T1, IDataRecord> applyT1, Action<T2, IDataRecord> applyT2)
    {
        _applyT1 = applyT1;
        _applyT2 = applyT2;
    }

    internal static MultiEntityMapper<T1, T2> Build(IDataReader reader)
    {
        var key = (typeof(T1), typeof(T2));

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var mapper = CreateMapper(reader);
        _cache.TryAdd(key, mapper);
        return mapper;
    }

    private static MultiEntityMapper<T1, T2> CreateMapper(IDataReader reader)
    {
        if (JauntyConfig.ReflectionMultiMapperResolver?.Invoke(typeof(T1), typeof(T2)) is Action<T1, T2, IDataRecord> combined)
        {
            return new MultiEntityMapper<T1, T2>(
                (t1, r) => combined(t1, default!, r),
                (t2, r) => combined(default!, t2, r)
            );
        }

        throw new InvalidOperationException(
            $"No multi-mapper found for types '{typeof(T1).Name}' and '{typeof(T2).Name}'. " +
            "Ensure 'Jaunty.Extensions.Reflection' is loaded for runtime multi-mapping.");
    }

    internal void ApplyT1(T1 target, IDataRecord record) => _applyT1(target, record);
    internal void ApplyT2(T2 target, IDataRecord record) => _applyT2(target, record);
}