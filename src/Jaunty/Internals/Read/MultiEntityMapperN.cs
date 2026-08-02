using System;
using System.Data;

using Jaunty.Configuration;
using Jaunty.Internals;

namespace Jaunty.Internals.Read;

/// <summary>
/// Validates what <see cref="JauntyConfig.ReflectionMultiMapperResolverN"/> hands back, before the
/// per-row closures start indexing it.
/// </summary>
/// <remarks>
/// AUD-R26. All five arities called the resolver - a public, settable
/// <c>Func&lt;Type[], IDataReader, Action&lt;object, IDataRecord&gt;[]&gt;</c> - and then indexed the
/// returned array at <c>0..N-1</c> from inside closures that run once per row, with no length check.
/// A third-party resolver returning fewer than N delegates produced an
/// <c>IndexOutOfRangeException</c> per row that mentioned neither the resolver, nor the arity, nor
/// the entity types: the one clue that would have pointed at the extension point was absent. The
/// in-repo implementation always returns exactly N, so nothing in the suite exercised it.
/// Checking once, where the array arrives, costs nothing per row and names the hook.
/// </remarks>
internal static class MultiEntityMapperNGuard
{
    /// <summary>
    /// Rejects a value-type entity before it can be mapped (AUD-R34-015).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>T1..T7</c> are constrained only to <c>new()</c>, so a struct entity compiles. It cannot
    /// work: the N-ary apply closures are <c>(t, r) =&gt; delegates[i](t!, r)</c> over
    /// <c>Action&lt;object, IDataRecord&gt;</c>, so the target is boxed and the setters run against
    /// the throwaway box; arity 2 passes the struct by value and loses the writes the same way. The
    /// caller's entity came back all-default with no exception and no wrong-looking SQL - the worst
    /// shape a defect can take. <c>MultiEntityMapper.cs</c>'s own AUD-R26 remarks already describe
    /// the boxed-copy behaviour for value types, so the possibility was known one file over.
    /// </para>
    /// <para>
    /// Supporting struct entities properly means ref-passing apply delegates through both the core
    /// and <c>Jaunty.Extensions.Reflection</c>, which <c>Action&lt;&gt;</c> cannot express. Until
    /// that exists this fails loudly rather than returning zeroed entities.
    /// </para>
    /// </remarks>
    public static void RequireReferenceTypes(Type[] types)
    {
        for (int i = 0; i < types.Length; i++)
        {
            if (!types[i].IsValueType) continue;

            throw new NotSupportedException(
                $"Multi-entity mapping requires reference-type entities, and '{types[i].Name}' " +
                $"(entity {i + 1} of {types.Length}: {DescribeTypes(types)}) is a value type. A " +
                "struct entity is populated through a copy, so every mapped value would be " +
                "discarded and the entity returned all-default. Declare the entity as a class.");
        }
    }

    public static Action<object, IDataRecord>[] Resolve(
        Func<Type[], IDataReader, Action<object, IDataRecord>[]> resolver, Type[] types, IDataReader reader)
    {
        RequireReferenceTypes(types);

        Action<object, IDataRecord>[]? delegates = resolver(types, reader);

        if (delegates is null)
            throw new InvalidOperationException(
                $"JauntyConfig.ReflectionMultiMapperResolverN returned null for arity {types.Length} " +
                $"({DescribeTypes(types)}). It must return one mapping delegate per entity type.");

        if (delegates.Length < types.Length)
            throw new InvalidOperationException(
                $"JauntyConfig.ReflectionMultiMapperResolverN returned {delegates.Length} delegate(s) " +
                $"for arity {types.Length} ({DescribeTypes(types)}). It must return one per entity " +
                "type, in the same order as the type array it was given.");

        for (int i = 0; i < types.Length; i++)
        {
            if (delegates[i] is null)
                throw new InvalidOperationException(
                    $"JauntyConfig.ReflectionMultiMapperResolverN returned a null delegate at index {i} " +
                    $"for arity {types.Length} ({DescribeTypes(types)}), where the mapper for " +
                    $"'{types[i].Name}' was expected.");
        }

        return delegates;
    }

    private static string DescribeTypes(Type[] types)
    {
        var names = new string[types.Length];
        for (int i = 0; i < types.Length; i++) names[i] = types[i].Name;
        return string.Join(", ", names);
    }
}

// ============================================================
//  Arity-3
// ============================================================

/// <summary>Hook for arity-3 multi-entity mapping. Actual implementation provided by Jaunty.Extensions.Reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3> where T1 : new() where T2 : new() where T3 : new()
{
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3>> _cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

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
        string key = BuildSchemaKey(reader);
        MultiEntityMapper<T1, T2, T3>? cached = _cache.Get(key);
        if (cached is not null) return cached;
        MultiEntityMapper<T1, T2, T3> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        int fieldCount = reader.FieldCount;
        var parts = new string[fieldCount + 1];
        parts[0] = fieldCount.ToString();
        for (int i = 0; i < fieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3) };
        Action<object, IDataRecord>[] delegates = MultiEntityMapperNGuard.Resolve(resolver, types, reader);
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
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3, T4>> _cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

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
        string key = BuildSchemaKey(reader);
        MultiEntityMapper<T1, T2, T3, T4>? cached = _cache.Get(key);
        if (cached is not null) return cached;
        MultiEntityMapper<T1, T2, T3, T4> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        int fieldCount = reader.FieldCount;
        var parts = new string[fieldCount + 1];
        parts[0] = fieldCount.ToString();
        for (int i = 0; i < fieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3, T4). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };
        Action<object, IDataRecord>[] delegates = MultiEntityMapperNGuard.Resolve(resolver, types, reader);
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
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3, T4, T5>> _cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

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
        string key = BuildSchemaKey(reader);
        MultiEntityMapper<T1, T2, T3, T4, T5>? cached = _cache.Get(key);
        if (cached is not null) return cached;
        MultiEntityMapper<T1, T2, T3, T4, T5> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        int fieldCount = reader.FieldCount;
        var parts = new string[fieldCount + 1];
        parts[0] = fieldCount.ToString();
        for (int i = 0; i < fieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3, T4, T5). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) };
        Action<object, IDataRecord>[] delegates = MultiEntityMapperNGuard.Resolve(resolver, types, reader);
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
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3, T4, T5, T6>> _cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

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
        string key = BuildSchemaKey(reader);
        MultiEntityMapper<T1, T2, T3, T4, T5, T6>? cached = _cache.Get(key);
        if (cached is not null) return cached;
        MultiEntityMapper<T1, T2, T3, T4, T5, T6> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        int fieldCount = reader.FieldCount;
        var parts = new string[fieldCount + 1];
        parts[0] = fieldCount.ToString();
        for (int i = 0; i < fieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5, T6> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3, T4, T5, T6). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6) };
        Action<object, IDataRecord>[] delegates = MultiEntityMapperNGuard.Resolve(resolver, types, reader);
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
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>> _cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

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
        string key = BuildSchemaKey(reader);
        MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>? cached = _cache.Get(key);
        if (cached is not null) return cached;
        MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> m = CreateMapper(reader);
        _cache.TryAdd(key, m);
        return m;
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        int fieldCount = reader.FieldCount;
        var parts = new string[fieldCount + 1];
        parts[0] = fieldCount.ToString();
        for (int i = 0; i < fieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> CreateMapper(IDataReader reader)
    {
        Func<Type[], IDataReader, Action<object, IDataRecord>[]>? resolver = JauntyConfig.ReflectionMultiMapperResolverN;
        if (resolver is null)
            throw new InvalidOperationException(
                "No N-ary multi-mapper found for (T1, T2, T3, T4, T5, T6, T7). Ensure Jaunty.Extensions.Reflection is loaded.");
        Type[] types = { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6), typeof(T7) };
        Action<object, IDataRecord>[] delegates = MultiEntityMapperNGuard.Resolve(resolver, types, reader);
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
