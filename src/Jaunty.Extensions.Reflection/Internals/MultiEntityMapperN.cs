using System;
using System.Collections.Generic;
using System.Data;

using Jaunty.Internals;

namespace Jaunty.Extensions.Reflection;

// Every BuildSchemaKey below joins the field count and column names with U+001F (unit
// separator) - a character that cannot appear in a column name, so two different reader
// schemas can never produce the same cache key. It is spelled as a \u001F escape rather
// than as a literal control character in the source (R24): a raw 0x1F byte is invisible in
// most editors and diffs, indistinguishable from an empty separator, and one stray formatting
// pass or copy-paste that dropped it would silently reintroduce the schema-key collision the
// AUD-R9-002-CORRECTION regression tests in MultiEntityMapperTests guard against. The arity-2
// implementation in MultiEntityMapper.cs uses the same escape.

// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//  Arity-3 mapper
// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

/// <summary>Maps three entity types from a single data reader using reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3>
    where T1 : new() where T2 : new() where T3 : new()
{
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    // AUD-R35-108 (round-35 batch 04a): StringComparer.Ordinal, matching the core mappers in
    // src/Jaunty/Internals/Read. This side used OrdinalIgnoreCase, with neither side saying why,
    // so two result sets differing only in column-name casing shared one cached mapper here and
    // got two entries there. Immaterial either way - the mapper binds by the ordinal position of
    // the key's column list - but the cache key is a contract, and Ordinal is the one that never
    // merges two schemas a provider would call distinct.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3>> Cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3)
    { _t1 = t1; _t2 = t2; _t3 = t3; }

    public static MultiEntityMapper<T1, T2, T3> Build(IDataReader reader)
    {
        string key = BuildSchemaKey(reader);
        return Cache.GetOrAdd(key, _ => Create(reader));
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        // AUD-R34-023: the generation is part of the key. Without it a JauntyConfig.ColumnNameResolver
        // change (or JauntyConfig.Reset(), both of which call ConfigurationGeneration.Invalidate())
        // left an already-built mapper in place for the process lifetime - MetadataCache<T> below
        // rebuilt and JauntyReflectionExtensions.MultiMapperCache above is ConfigurationScoped, but
        // the delegate that cache rebuilds calls straight back into this one, which returned the
        // pre-change mapper for a column shape it had seen before.
        var parts = new string[reader.FieldCount + 1];
        parts[0] = ConfigurationGeneration.Current.ToString() + "|" + reader.FieldCount.ToString();
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3> Create(IDataReader reader)
    {
        var c = new HashSet<int>();
        (PropertySetter<T1>[] t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c);
        foreach (int o in o1) c.Add(o);
        (PropertySetter<T2>[] t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c);
        foreach (int o in o2) c.Add(o);
        (PropertySetter<T3>[] t3, _) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c);
        return new MultiEntityMapper<T1, T2, T3>(t1, t2, t3);
    }

    public void ApplyT1(T1 t, IDataRecord r) { for (int i = 0; i < _t1.Length; i++) _t1[i].Set(t, r); }
    public void ApplyT2(T2 t, IDataRecord r) { for (int i = 0; i < _t2.Length; i++) _t2[i].Set(t, r); }
    public void ApplyT3(T3 t, IDataRecord r) { for (int i = 0; i < _t3.Length; i++) _t3[i].Set(t, r); }
}

// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//  Arity-4 mapper
// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

/// <summary>Maps four entity types from a single data reader using reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3, T4>
    where T1 : new() where T2 : new() where T3 : new() where T4 : new()
{
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    // AUD-R35-108 (round-35 batch 04a): StringComparer.Ordinal, matching the core mappers in
    // src/Jaunty/Internals/Read. This side used OrdinalIgnoreCase, with neither side saying why,
    // so two result sets differing only in column-name casing shared one cached mapper here and
    // got two entries there. Immaterial either way - the mapper binds by the ordinal position of
    // the key's column list - but the cache key is a contract, and Ordinal is the one that never
    // merges two schemas a provider would call distinct.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3, T4>> Cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;
    private readonly PropertySetter<T4>[] _t4;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3, PropertySetter<T4>[] t4)
    { _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; }

    public static MultiEntityMapper<T1, T2, T3, T4> Build(IDataReader reader)
    {
        string key = BuildSchemaKey(reader);
        return Cache.GetOrAdd(key, _ => Create(reader));
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        // AUD-R34-023: the generation is part of the key. Without it a JauntyConfig.ColumnNameResolver
        // change (or JauntyConfig.Reset(), both of which call ConfigurationGeneration.Invalidate())
        // left an already-built mapper in place for the process lifetime - MetadataCache<T> below
        // rebuilt and JauntyReflectionExtensions.MultiMapperCache above is ConfigurationScoped, but
        // the delegate that cache rebuilds calls straight back into this one, which returned the
        // pre-change mapper for a column shape it had seen before.
        var parts = new string[reader.FieldCount + 1];
        parts[0] = ConfigurationGeneration.Current.ToString() + "|" + reader.FieldCount.ToString();
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4> Create(IDataReader reader)
    {
        var c = new HashSet<int>();
        (PropertySetter<T1>[] t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c);
        foreach (int o in o1) c.Add(o);
        (PropertySetter<T2>[] t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c);
        foreach (int o in o2) c.Add(o);
        (PropertySetter<T3>[] t3, int[] o3) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c);
        foreach (int o in o3) c.Add(o);
        (PropertySetter<T4>[] t4, _) = MultiEntityMapperCore.GetSettersExcluding<T4>(reader, c);
        return new MultiEntityMapper<T1, T2, T3, T4>(t1, t2, t3, t4);
    }

    public void ApplyT1(T1 t, IDataRecord r) { for (int i = 0; i < _t1.Length; i++) _t1[i].Set(t, r); }
    public void ApplyT2(T2 t, IDataRecord r) { for (int i = 0; i < _t2.Length; i++) _t2[i].Set(t, r); }
    public void ApplyT3(T3 t, IDataRecord r) { for (int i = 0; i < _t3.Length; i++) _t3[i].Set(t, r); }
    public void ApplyT4(T4 t, IDataRecord r) { for (int i = 0; i < _t4.Length; i++) _t4[i].Set(t, r); }
}

// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//  Arity-5 mapper
// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

/// <summary>Maps five entity types from a single data reader using reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3, T4, T5>
    where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
{
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    // AUD-R35-108 (round-35 batch 04a): StringComparer.Ordinal, matching the core mappers in
    // src/Jaunty/Internals/Read. This side used OrdinalIgnoreCase, with neither side saying why,
    // so two result sets differing only in column-name casing shared one cached mapper here and
    // got two entries there. Immaterial either way - the mapper binds by the ordinal position of
    // the key's column list - but the cache key is a contract, and Ordinal is the one that never
    // merges two schemas a provider would call distinct.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3, T4, T5>> Cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;
    private readonly PropertySetter<T4>[] _t4;
    private readonly PropertySetter<T5>[] _t5;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3, PropertySetter<T4>[] t4, PropertySetter<T5>[] t5)
    { _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; _t5 = t5; }

    public static MultiEntityMapper<T1, T2, T3, T4, T5> Build(IDataReader reader)
    {
        string key = BuildSchemaKey(reader);
        return Cache.GetOrAdd(key, _ => Create(reader));
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        // AUD-R34-023: the generation is part of the key. Without it a JauntyConfig.ColumnNameResolver
        // change (or JauntyConfig.Reset(), both of which call ConfigurationGeneration.Invalidate())
        // left an already-built mapper in place for the process lifetime - MetadataCache<T> below
        // rebuilt and JauntyReflectionExtensions.MultiMapperCache above is ConfigurationScoped, but
        // the delegate that cache rebuilds calls straight back into this one, which returned the
        // pre-change mapper for a column shape it had seen before.
        var parts = new string[reader.FieldCount + 1];
        parts[0] = ConfigurationGeneration.Current.ToString() + "|" + reader.FieldCount.ToString();
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5> Create(IDataReader reader)
    {
        var c = new HashSet<int>();
        (PropertySetter<T1>[] t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c);
        foreach (int o in o1) c.Add(o);
        (PropertySetter<T2>[] t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c);
        foreach (int o in o2) c.Add(o);
        (PropertySetter<T3>[] t3, int[] o3) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c);
        foreach (int o in o3) c.Add(o);
        (PropertySetter<T4>[] t4, int[] o4) = MultiEntityMapperCore.GetSettersExcluding<T4>(reader, c);
        foreach (int o in o4) c.Add(o);
        (PropertySetter<T5>[] t5, _) = MultiEntityMapperCore.GetSettersExcluding<T5>(reader, c);
        return new MultiEntityMapper<T1, T2, T3, T4, T5>(t1, t2, t3, t4, t5);
    }

    public void ApplyT1(T1 t, IDataRecord r) { for (int i = 0; i < _t1.Length; i++) _t1[i].Set(t, r); }
    public void ApplyT2(T2 t, IDataRecord r) { for (int i = 0; i < _t2.Length; i++) _t2[i].Set(t, r); }
    public void ApplyT3(T3 t, IDataRecord r) { for (int i = 0; i < _t3.Length; i++) _t3[i].Set(t, r); }
    public void ApplyT4(T4 t, IDataRecord r) { for (int i = 0; i < _t4.Length; i++) _t4[i].Set(t, r); }
    public void ApplyT5(T5 t, IDataRecord r) { for (int i = 0; i < _t5.Length; i++) _t5[i].Set(t, r); }
}

// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//  Arity-6 mapper
// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

/// <summary>Maps six entity types from a single data reader using reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3, T4, T5, T6>
    where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
{
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    // AUD-R35-108 (round-35 batch 04a): StringComparer.Ordinal, matching the core mappers in
    // src/Jaunty/Internals/Read. This side used OrdinalIgnoreCase, with neither side saying why,
    // so two result sets differing only in column-name casing shared one cached mapper here and
    // got two entries there. Immaterial either way - the mapper binds by the ordinal position of
    // the key's column list - but the cache key is a contract, and Ordinal is the one that never
    // merges two schemas a provider would call distinct.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3, T4, T5, T6>> Cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;
    private readonly PropertySetter<T4>[] _t4;
    private readonly PropertySetter<T5>[] _t5;
    private readonly PropertySetter<T6>[] _t6;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3, PropertySetter<T4>[] t4, PropertySetter<T5>[] t5, PropertySetter<T6>[] t6)
    { _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; _t5 = t5; _t6 = t6; }

    public static MultiEntityMapper<T1, T2, T3, T4, T5, T6> Build(IDataReader reader)
    {
        string key = BuildSchemaKey(reader);
        return Cache.GetOrAdd(key, _ => Create(reader));
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        // AUD-R34-023: the generation is part of the key. Without it a JauntyConfig.ColumnNameResolver
        // change (or JauntyConfig.Reset(), both of which call ConfigurationGeneration.Invalidate())
        // left an already-built mapper in place for the process lifetime - MetadataCache<T> below
        // rebuilt and JauntyReflectionExtensions.MultiMapperCache above is ConfigurationScoped, but
        // the delegate that cache rebuilds calls straight back into this one, which returned the
        // pre-change mapper for a column shape it had seen before.
        var parts = new string[reader.FieldCount + 1];
        parts[0] = ConfigurationGeneration.Current.ToString() + "|" + reader.FieldCount.ToString();
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5, T6> Create(IDataReader reader)
    {
        var c = new HashSet<int>();
        (PropertySetter<T1>[] t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c);
        foreach (int o in o1) c.Add(o);
        (PropertySetter<T2>[] t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c);
        foreach (int o in o2) c.Add(o);
        (PropertySetter<T3>[] t3, int[] o3) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c);
        foreach (int o in o3) c.Add(o);
        (PropertySetter<T4>[] t4, int[] o4) = MultiEntityMapperCore.GetSettersExcluding<T4>(reader, c);
        foreach (int o in o4) c.Add(o);
        (PropertySetter<T5>[] t5, int[] o5) = MultiEntityMapperCore.GetSettersExcluding<T5>(reader, c);
        foreach (int o in o5) c.Add(o);
        (PropertySetter<T6>[] t6, _) = MultiEntityMapperCore.GetSettersExcluding<T6>(reader, c);
        return new MultiEntityMapper<T1, T2, T3, T4, T5, T6>(t1, t2, t3, t4, t5, t6);
    }

    public void ApplyT1(T1 t, IDataRecord r) { for (int i = 0; i < _t1.Length; i++) _t1[i].Set(t, r); }
    public void ApplyT2(T2 t, IDataRecord r) { for (int i = 0; i < _t2.Length; i++) _t2[i].Set(t, r); }
    public void ApplyT3(T3 t, IDataRecord r) { for (int i = 0; i < _t3.Length; i++) _t3[i].Set(t, r); }
    public void ApplyT4(T4 t, IDataRecord r) { for (int i = 0; i < _t4.Length; i++) _t4[i].Set(t, r); }
    public void ApplyT5(T5 t, IDataRecord r) { for (int i = 0; i < _t5.Length; i++) _t5[i].Set(t, r); }
    public void ApplyT6(T6 t, IDataRecord r) { for (int i = 0; i < _t6.Length; i++) _t6[i].Set(t, r); }
}

// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//  Arity-7 mapper
// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

/// <summary>Maps seven entity types from a single data reader using reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>
    where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
{
    // AUD-R26-053: bounded. The key is the result set's column-name list - caller-controlled through
    // the SELECT list - and this was a ConcurrentDictionary that nothing ever removed from, so every
    // distinct shape left a permanent entry. See BoundedCache.SchemaCacheMaxEntries for the cap.
    // AUD-R35-108 (round-35 batch 04a): StringComparer.Ordinal, matching the core mappers in
    // src/Jaunty/Internals/Read. This side used OrdinalIgnoreCase, with neither side saying why,
    // so two result sets differing only in column-name casing shared one cached mapper here and
    // got two entries there. Immaterial either way - the mapper binds by the ordinal position of
    // the key's column list - but the cache key is a contract, and Ordinal is the one that never
    // merges two schemas a provider would call distinct.
    private static readonly BoundedCache<string, MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>> Cache =
        new(StringComparer.Ordinal, BoundedCacheLimits.SchemaCacheMaxEntries);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;
    private readonly PropertySetter<T4>[] _t4;
    private readonly PropertySetter<T5>[] _t5;
    private readonly PropertySetter<T6>[] _t6;
    private readonly PropertySetter<T7>[] _t7;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3, PropertySetter<T4>[] t4, PropertySetter<T5>[] t5, PropertySetter<T6>[] t6, PropertySetter<T7>[] t7)
    { _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; _t5 = t5; _t6 = t6; _t7 = t7; }

    public static MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> Build(IDataReader reader)
    {
        string key = BuildSchemaKey(reader);
        return Cache.GetOrAdd(key, _ => Create(reader));
    }

    private static string BuildSchemaKey(IDataReader reader)
    {
        // AUD-R34-023: the generation is part of the key. Without it a JauntyConfig.ColumnNameResolver
        // change (or JauntyConfig.Reset(), both of which call ConfigurationGeneration.Invalidate())
        // left an already-built mapper in place for the process lifetime - MetadataCache<T> below
        // rebuilt and JauntyReflectionExtensions.MultiMapperCache above is ConfigurationScoped, but
        // the delegate that cache rebuilds calls straight back into this one, which returned the
        // pre-change mapper for a column shape it had seen before.
        var parts = new string[reader.FieldCount + 1];
        parts[0] = ConfigurationGeneration.Current.ToString() + "|" + reader.FieldCount.ToString();
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 1] = reader.GetName(i) ?? string.Empty;
        return string.Join("\u001F", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> Create(IDataReader reader)
    {
        var c = new HashSet<int>();
        (PropertySetter<T1>[] t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c);
        foreach (int o in o1) c.Add(o);
        (PropertySetter<T2>[] t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c);
        foreach (int o in o2) c.Add(o);
        (PropertySetter<T3>[] t3, int[] o3) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c);
        foreach (int o in o3) c.Add(o);
        (PropertySetter<T4>[] t4, int[] o4) = MultiEntityMapperCore.GetSettersExcluding<T4>(reader, c);
        foreach (int o in o4) c.Add(o);
        (PropertySetter<T5>[] t5, int[] o5) = MultiEntityMapperCore.GetSettersExcluding<T5>(reader, c);
        foreach (int o in o5) c.Add(o);
        (PropertySetter<T6>[] t6, int[] o6) = MultiEntityMapperCore.GetSettersExcluding<T6>(reader, c);
        foreach (int o in o6) c.Add(o);
        (PropertySetter<T7>[] t7, _) = MultiEntityMapperCore.GetSettersExcluding<T7>(reader, c);
        return new MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>(t1, t2, t3, t4, t5, t6, t7);
    }

    public void ApplyT1(T1 t, IDataRecord r) { for (int i = 0; i < _t1.Length; i++) _t1[i].Set(t, r); }
    public void ApplyT2(T2 t, IDataRecord r) { for (int i = 0; i < _t2.Length; i++) _t2[i].Set(t, r); }
    public void ApplyT3(T3 t, IDataRecord r) { for (int i = 0; i < _t3.Length; i++) _t3[i].Set(t, r); }
    public void ApplyT4(T4 t, IDataRecord r) { for (int i = 0; i < _t4.Length; i++) _t4[i].Set(t, r); }
    public void ApplyT5(T5 t, IDataRecord r) { for (int i = 0; i < _t5.Length; i++) _t5[i].Set(t, r); }
    public void ApplyT6(T6 t, IDataRecord r) { for (int i = 0; i < _t6.Length; i++) _t6[i].Set(t, r); }
    public void ApplyT7(T7 t, IDataRecord r) { for (int i = 0; i < _t7.Length; i++) _t7[i].Set(t, r); }
}
