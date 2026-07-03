using System;
using System.Collections.Concurrent;
using System.Data;

using Jaunty.Configuration;

namespace Jaunty.Internals.Read;

// ============================================================
//  Arity-3
// ============================================================

/// <summary>Hook for arity-3 multi-entity mapping. Actual implementation provided by Jaunty.Extensions.Reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3> where T1 : new() where T2 : new() where T3 : new()
{
    private static readonly ConcurrentDictionary<(Type, Type, Type), MultiEntityMapper<T1, T2, T3>> _cache = new();

    private readonly Action<T1, IDataRecord> _applyT1;
    private readonly Action<T2, IDataRecord> _applyT2;
    private readonly Action<T3, IDataRecord> _applyT3;

    private MultiEntityMapper(Action<T1, IDataRecord> applyT1, Action<T2, IDataRecord> applyT2, Action<T3, IDataRecord> applyT3)
    {
        _applyT1 = applyT1;
        _applyT2 = applyT2;
        _applyT3 = applyT3;
    }

    internal static MultiEntityMapper<T1, T2, T3> Build(IDataReader reader)
    {
        (Type, Type, Type) key = (typeof(T1), typeof(T2), typeof(T3));
        if (_cache.TryGetValue(key, out MultiEntityMapper<T1, T2, T3>? cached)) return cached;
        MultiEntityMapper<T1, T2, T3> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static MultiEntityMapper<T1, T2, T3> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3) };
        Action<object, IDataRecord>[] delegates = resolver(types, reader);
        int idx1 = 0;
        Action<T1, IDataRecord> applyT1 = (t, r) => delegates[idx1](t!, r);
        int idx2 = 1;
        Action<T2, IDataRecord> applyT2 = (t, r) => delegates[idx2](t!, r);
        int idx3 = 2;
        Action<T3, IDataRecord> applyT3 = (t, r) => delegates[idx3](t!, r);
        return new MultiEntityMapper<T1, T2, T3>(applyT1, applyT2, applyT3);
    }

    internal void ApplyT1(T1 target, IDataRecord record) => _applyT1(target, record);
    internal void ApplyT2(T2 target, IDataRecord record) => _applyT2(target, record);
    internal void ApplyT3(T3 target, IDataRecord record) => _applyT3(target, record);
}

// ============================================================
//  Arity-4
// ============================================================

/// <summary>Hook for arity-4 multi-entity mapping. Actual implementation provided by Jaunty.Extensions.Reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3, T4> where T1 : new() where T2 : new() where T3 : new() where T4 : new()
{
    private static readonly ConcurrentDictionary<(Type, Type, Type, Type), MultiEntityMapper<T1, T2, T3, T4>> _cache = new();

    private readonly Action<T1, IDataRecord> _applyT1;
    private readonly Action<T2, IDataRecord> _applyT2;
    private readonly Action<T3, IDataRecord> _applyT3;
    private readonly Action<T4, IDataRecord> _applyT4;

    private MultiEntityMapper(Action<T1, IDataRecord> applyT1, Action<T2, IDataRecord> applyT2, Action<T3, IDataRecord> applyT3, Action<T4, IDataRecord> applyT4)
    {
        _applyT1 = applyT1;
        _applyT2 = applyT2;
        _applyT3 = applyT3;
        _applyT4 = applyT4;
    }

    internal static MultiEntityMapper<T1, T2, T3, T4> Build(IDataReader reader)
    {
        (Type, Type, Type, Type) key = (typeof(T1), typeof(T2), typeof(T3), typeof(T4));
        if (_cache.TryGetValue(key, out MultiEntityMapper<T1, T2, T3, T4>? cached)) return cached;
        MultiEntityMapper<T1, T2, T3, T4> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static MultiEntityMapper<T1, T2, T3, T4> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3, T4). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };
        Action<object, IDataRecord>[] delegates = resolver(types, reader);
        int idx1 = 0;
        Action<T1, IDataRecord> applyT1 = (t, r) => delegates[idx1](t!, r);
        int idx2 = 1;
        Action<T2, IDataRecord> applyT2 = (t, r) => delegates[idx2](t!, r);
        int idx3 = 2;
        Action<T3, IDataRecord> applyT3 = (t, r) => delegates[idx3](t!, r);
        int idx4 = 3;
        Action<T4, IDataRecord> applyT4 = (t, r) => delegates[idx4](t!, r);
        return new MultiEntityMapper<T1, T2, T3, T4>(applyT1, applyT2, applyT3, applyT4);
    }

    internal void ApplyT1(T1 target, IDataRecord record) => _applyT1(target, record);
    internal void ApplyT2(T2 target, IDataRecord record) => _applyT2(target, record);
    internal void ApplyT3(T3 target, IDataRecord record) => _applyT3(target, record);
    internal void ApplyT4(T4 target, IDataRecord record) => _applyT4(target, record);
}

// ============================================================
//  Arity-5
// ============================================================

/// <summary>Hook for arity-5 multi-entity mapping. Actual implementation provided by Jaunty.Extensions.Reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3, T4, T5> where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
{
    private static readonly ConcurrentDictionary<(Type, Type, Type, Type, Type), MultiEntityMapper<T1, T2, T3, T4, T5>> _cache = new();

    private readonly Action<T1, IDataRecord> _applyT1;
    private readonly Action<T2, IDataRecord> _applyT2;
    private readonly Action<T3, IDataRecord> _applyT3;
    private readonly Action<T4, IDataRecord> _applyT4;
    private readonly Action<T5, IDataRecord> _applyT5;

    private MultiEntityMapper(Action<T1, IDataRecord> applyT1, Action<T2, IDataRecord> applyT2, Action<T3, IDataRecord> applyT3, Action<T4, IDataRecord> applyT4, Action<T5, IDataRecord> applyT5)
    {
        _applyT1 = applyT1;
        _applyT2 = applyT2;
        _applyT3 = applyT3;
        _applyT4 = applyT4;
        _applyT5 = applyT5;
    }

    internal static MultiEntityMapper<T1, T2, T3, T4, T5> Build(IDataReader reader)
    {
        (Type, Type, Type, Type, Type) key = (typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5));
        if (_cache.TryGetValue(key, out MultiEntityMapper<T1, T2, T3, T4, T5>? cached)) return cached;
        MultiEntityMapper<T1, T2, T3, T4, T5> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3, T4, T5). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) };
        Action<object, IDataRecord>[] delegates = resolver(types, reader);
        int idx1 = 0;
        Action<T1, IDataRecord> applyT1 = (t, r) => delegates[idx1](t!, r);
        int idx2 = 1;
        Action<T2, IDataRecord> applyT2 = (t, r) => delegates[idx2](t!, r);
        int idx3 = 2;
        Action<T3, IDataRecord> applyT3 = (t, r) => delegates[idx3](t!, r);
        int idx4 = 3;
        Action<T4, IDataRecord> applyT4 = (t, r) => delegates[idx4](t!, r);
        int idx5 = 4;
        Action<T5, IDataRecord> applyT5 = (t, r) => delegates[idx5](t!, r);
        return new MultiEntityMapper<T1, T2, T3, T4, T5>(applyT1, applyT2, applyT3, applyT4, applyT5);
    }

    internal void ApplyT1(T1 target, IDataRecord record) => _applyT1(target, record);
    internal void ApplyT2(T2 target, IDataRecord record) => _applyT2(target, record);
    internal void ApplyT3(T3 target, IDataRecord record) => _applyT3(target, record);
    internal void ApplyT4(T4 target, IDataRecord record) => _applyT4(target, record);
    internal void ApplyT5(T5 target, IDataRecord record) => _applyT5(target, record);
}

// ============================================================
//  Arity-6
// ============================================================

/// <summary>Hook for arity-6 multi-entity mapping. Actual implementation provided by Jaunty.Extensions.Reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3, T4, T5, T6> where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
{
    private static readonly ConcurrentDictionary<(Type, Type, Type, Type, Type, Type), MultiEntityMapper<T1, T2, T3, T4, T5, T6>> _cache = new();

    private readonly Action<T1, IDataRecord> _applyT1;
    private readonly Action<T2, IDataRecord> _applyT2;
    private readonly Action<T3, IDataRecord> _applyT3;
    private readonly Action<T4, IDataRecord> _applyT4;
    private readonly Action<T5, IDataRecord> _applyT5;
    private readonly Action<T6, IDataRecord> _applyT6;

    private MultiEntityMapper(Action<T1, IDataRecord> applyT1, Action<T2, IDataRecord> applyT2, Action<T3, IDataRecord> applyT3, Action<T4, IDataRecord> applyT4, Action<T5, IDataRecord> applyT5, Action<T6, IDataRecord> applyT6)
    {
        _applyT1 = applyT1;
        _applyT2 = applyT2;
        _applyT3 = applyT3;
        _applyT4 = applyT4;
        _applyT5 = applyT5;
        _applyT6 = applyT6;
    }

    internal static MultiEntityMapper<T1, T2, T3, T4, T5, T6> Build(IDataReader reader)
    {
        (Type, Type, Type, Type, Type, Type) key = (typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6));
        if (_cache.TryGetValue(key, out MultiEntityMapper<T1, T2, T3, T4, T5, T6>? cached)) return cached;
        MultiEntityMapper<T1, T2, T3, T4, T5, T6> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5, T6> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3, T4, T5, T6). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6) };
        Action<object, IDataRecord>[] delegates = resolver(types, reader);
        int idx1 = 0;
        Action<T1, IDataRecord> applyT1 = (t, r) => delegates[idx1](t!, r);
        int idx2 = 1;
        Action<T2, IDataRecord> applyT2 = (t, r) => delegates[idx2](t!, r);
        int idx3 = 2;
        Action<T3, IDataRecord> applyT3 = (t, r) => delegates[idx3](t!, r);
        int idx4 = 3;
        Action<T4, IDataRecord> applyT4 = (t, r) => delegates[idx4](t!, r);
        int idx5 = 4;
        Action<T5, IDataRecord> applyT5 = (t, r) => delegates[idx5](t!, r);
        int idx6 = 5;
        Action<T6, IDataRecord> applyT6 = (t, r) => delegates[idx6](t!, r);
        return new MultiEntityMapper<T1, T2, T3, T4, T5, T6>(applyT1, applyT2, applyT3, applyT4, applyT5, applyT6);
    }

    internal void ApplyT1(T1 target, IDataRecord record) => _applyT1(target, record);
    internal void ApplyT2(T2 target, IDataRecord record) => _applyT2(target, record);
    internal void ApplyT3(T3 target, IDataRecord record) => _applyT3(target, record);
    internal void ApplyT4(T4 target, IDataRecord record) => _applyT4(target, record);
    internal void ApplyT5(T5 target, IDataRecord record) => _applyT5(target, record);
    internal void ApplyT6(T6 target, IDataRecord record) => _applyT6(target, record);
}

// ============================================================
//  Arity-7
// ============================================================

/// <summary>Hook for arity-7 multi-entity mapping. Actual implementation provided by Jaunty.Extensions.Reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
{
    private static readonly ConcurrentDictionary<(Type, Type, Type, Type, Type, Type, Type), MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>> _cache = new();

    private readonly Action<T1, IDataRecord> _applyT1;
    private readonly Action<T2, IDataRecord> _applyT2;
    private readonly Action<T3, IDataRecord> _applyT3;
    private readonly Action<T4, IDataRecord> _applyT4;
    private readonly Action<T5, IDataRecord> _applyT5;
    private readonly Action<T6, IDataRecord> _applyT6;
    private readonly Action<T7, IDataRecord> _applyT7;

    private MultiEntityMapper(Action<T1, IDataRecord> applyT1, Action<T2, IDataRecord> applyT2, Action<T3, IDataRecord> applyT3, Action<T4, IDataRecord> applyT4, Action<T5, IDataRecord> applyT5, Action<T6, IDataRecord> applyT6, Action<T7, IDataRecord> applyT7)
    {
        _applyT1 = applyT1;
        _applyT2 = applyT2;
        _applyT3 = applyT3;
        _applyT4 = applyT4;
        _applyT5 = applyT5;
        _applyT6 = applyT6;
        _applyT7 = applyT7;
    }

    internal static MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> Build(IDataReader reader)
    {
        (Type, Type, Type, Type, Type, Type, Type) key = (typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7));
        if (_cache.TryGetValue(key, out MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>? cached)) return cached;
        MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3, T4, T5, T6, T7). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7) };
        Action<object, IDataRecord>[] delegates = resolver(types, reader);
        int idx1 = 0;
        Action<T1, IDataRecord> applyT1 = (t, r) => delegates[idx1](t!, r);
        int idx2 = 1;
        Action<T2, IDataRecord> applyT2 = (t, r) => delegates[idx2](t!, r);
        int idx3 = 2;
        Action<T3, IDataRecord> applyT3 = (t, r) => delegates[idx3](t!, r);
        int idx4 = 3;
        Action<T4, IDataRecord> applyT4 = (t, r) => delegates[idx4](t!, r);
        int idx5 = 4;
        Action<T5, IDataRecord> applyT5 = (t, r) => delegates[idx5](t!, r);
        int idx6 = 5;
        Action<T6, IDataRecord> applyT6 = (t, r) => delegates[idx6](t!, r);
        int idx7 = 6;
        Action<T7, IDataRecord> applyT7 = (t, r) => delegates[idx7](t!, r);
        return new MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>(applyT1, applyT2, applyT3, applyT4, applyT5, applyT6, applyT7);
    }

    internal void ApplyT1(T1 target, IDataRecord record) => _applyT1(target, record);
    internal void ApplyT2(T2 target, IDataRecord record) => _applyT2(target, record);
    internal void ApplyT3(T3 target, IDataRecord record) => _applyT3(target, record);
    internal void ApplyT4(T4 target, IDataRecord record) => _applyT4(target, record);
    internal void ApplyT5(T5 target, IDataRecord record) => _applyT5(target, record);
    internal void ApplyT6(T6 target, IDataRecord record) => _applyT6(target, record);
    internal void ApplyT7(T7 target, IDataRecord record) => _applyT7(target, record);
}
