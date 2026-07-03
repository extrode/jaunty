using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;

namespace Jaunty.Extensions.Reflection;

// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
//  Arity-3 mapper
// ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

/// <summary>Maps three entity types from a single data reader using reflection.</summary>
internal sealed class MultiEntityMapper<T1, T2, T3>
    where T1 : new() where T2 : new() where T3 : new()
{
    private static readonly ConcurrentDictionary<string, MultiEntityMapper<T1, T2, T3>> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3)
    { _t1 = t1; _t2 = t2; _t3 = t3; }

    public static MultiEntityMapper<T1, T2, T3> Build(
        IDataReader reader,
        Func<IDataReader, T1>? m1 = null,
        Func<IDataReader, T2>? m2 = null,
        Func<IDataReader, T3>? m3 = null)
    {
        string key = BuildSchemaKey(reader, m1, m2, m3);
        return Cache.GetOrAdd(key, _ => Create(reader, m1, m2, m3));
    }

    private static string BuildSchemaKey(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3)
    {
        var parts = new string[reader.FieldCount + 4];
        parts[0] = reader.FieldCount.ToString();
        parts[1] = m1 is null ? "0" : "1";
        parts[2] = m2 is null ? "0" : "1";
        parts[3] = m3 is null ? "0" : "1";
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 4] = reader.GetName(i) ?? string.Empty;
        return string.Join("", parts);
    }

    private static MultiEntityMapper<T1, T2, T3> Create(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3)
    {
        var c = new HashSet<int>();
        PropertySetter<T1>[] t1;
        if (m1 is null) { (t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c); foreach (int o in o1) c.Add(o); }
        else t1 = Array.Empty<PropertySetter<T1>>();
        PropertySetter<T2>[] t2;
        if (m2 is null) { (t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c); foreach (int o in o2) c.Add(o); }
        else t2 = Array.Empty<PropertySetter<T2>>();
        PropertySetter<T3>[] t3;
        if (m3 is null) { (t3, _) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c); }
        else t3 = Array.Empty<PropertySetter<T3>>();
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
    private static readonly ConcurrentDictionary<string, MultiEntityMapper<T1, T2, T3, T4>> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;
    private readonly PropertySetter<T4>[] _t4;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3, PropertySetter<T4>[] t4)
    { _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; }

    public static MultiEntityMapper<T1, T2, T3, T4> Build(
        IDataReader reader,
        Func<IDataReader, T1>? m1 = null,
        Func<IDataReader, T2>? m2 = null,
        Func<IDataReader, T3>? m3 = null,
        Func<IDataReader, T4>? m4 = null)
    {
        string key = BuildSchemaKey(reader, m1, m2, m3, m4);
        return Cache.GetOrAdd(key, _ => Create(reader, m1, m2, m3, m4));
    }

    private static string BuildSchemaKey(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3, Func<IDataReader, T4>? m4)
    {
        var parts = new string[reader.FieldCount + 5];
        parts[0] = reader.FieldCount.ToString();
        parts[1] = m1 is null ? "0" : "1";
        parts[2] = m2 is null ? "0" : "1";
        parts[3] = m3 is null ? "0" : "1";
        parts[4] = m4 is null ? "0" : "1";
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 5] = reader.GetName(i) ?? string.Empty;
        return string.Join("", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4> Create(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3, Func<IDataReader, T4>? m4)
    {
        var c = new HashSet<int>();
        PropertySetter<T1>[] t1;
        if (m1 is null) { (t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c); foreach (int o in o1) c.Add(o); }
        else t1 = Array.Empty<PropertySetter<T1>>();
        PropertySetter<T2>[] t2;
        if (m2 is null) { (t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c); foreach (int o in o2) c.Add(o); }
        else t2 = Array.Empty<PropertySetter<T2>>();
        PropertySetter<T3>[] t3;
        if (m3 is null) { (t3, int[] o3) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c); foreach (int o in o3) c.Add(o); }
        else t3 = Array.Empty<PropertySetter<T3>>();
        PropertySetter<T4>[] t4;
        if (m4 is null) { (t4, _) = MultiEntityMapperCore.GetSettersExcluding<T4>(reader, c); }
        else t4 = Array.Empty<PropertySetter<T4>>();
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
    private static readonly ConcurrentDictionary<string, MultiEntityMapper<T1, T2, T3, T4, T5>> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;
    private readonly PropertySetter<T4>[] _t4;
    private readonly PropertySetter<T5>[] _t5;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3, PropertySetter<T4>[] t4, PropertySetter<T5>[] t5)
    { _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; _t5 = t5; }

    public static MultiEntityMapper<T1, T2, T3, T4, T5> Build(
        IDataReader reader,
        Func<IDataReader, T1>? m1 = null,
        Func<IDataReader, T2>? m2 = null,
        Func<IDataReader, T3>? m3 = null,
        Func<IDataReader, T4>? m4 = null,
        Func<IDataReader, T5>? m5 = null)
    {
        string key = BuildSchemaKey(reader, m1, m2, m3, m4, m5);
        return Cache.GetOrAdd(key, _ => Create(reader, m1, m2, m3, m4, m5));
    }

    private static string BuildSchemaKey(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3, Func<IDataReader, T4>? m4, Func<IDataReader, T5>? m5)
    {
        var parts = new string[reader.FieldCount + 6];
        parts[0] = reader.FieldCount.ToString();
        parts[1] = m1 is null ? "0" : "1";
        parts[2] = m2 is null ? "0" : "1";
        parts[3] = m3 is null ? "0" : "1";
        parts[4] = m4 is null ? "0" : "1";
        parts[5] = m5 is null ? "0" : "1";
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 6] = reader.GetName(i) ?? string.Empty;
        return string.Join("", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5> Create(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3, Func<IDataReader, T4>? m4, Func<IDataReader, T5>? m5)
    {
        var c = new HashSet<int>();
        PropertySetter<T1>[] t1;
        if (m1 is null) { (t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c); foreach (int o in o1) c.Add(o); }
        else t1 = Array.Empty<PropertySetter<T1>>();
        PropertySetter<T2>[] t2;
        if (m2 is null) { (t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c); foreach (int o in o2) c.Add(o); }
        else t2 = Array.Empty<PropertySetter<T2>>();
        PropertySetter<T3>[] t3;
        if (m3 is null) { (t3, int[] o3) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c); foreach (int o in o3) c.Add(o); }
        else t3 = Array.Empty<PropertySetter<T3>>();
        PropertySetter<T4>[] t4;
        if (m4 is null) { (t4, int[] o4) = MultiEntityMapperCore.GetSettersExcluding<T4>(reader, c); foreach (int o in o4) c.Add(o); }
        else t4 = Array.Empty<PropertySetter<T4>>();
        PropertySetter<T5>[] t5;
        if (m5 is null) { (t5, _) = MultiEntityMapperCore.GetSettersExcluding<T5>(reader, c); }
        else t5 = Array.Empty<PropertySetter<T5>>();
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
    private static readonly ConcurrentDictionary<string, MultiEntityMapper<T1, T2, T3, T4, T5, T6>> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;
    private readonly PropertySetter<T4>[] _t4;
    private readonly PropertySetter<T5>[] _t5;
    private readonly PropertySetter<T6>[] _t6;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3, PropertySetter<T4>[] t4, PropertySetter<T5>[] t5, PropertySetter<T6>[] t6)
    { _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; _t5 = t5; _t6 = t6; }

    public static MultiEntityMapper<T1, T2, T3, T4, T5, T6> Build(
        IDataReader reader,
        Func<IDataReader, T1>? m1 = null,
        Func<IDataReader, T2>? m2 = null,
        Func<IDataReader, T3>? m3 = null,
        Func<IDataReader, T4>? m4 = null,
        Func<IDataReader, T5>? m5 = null,
        Func<IDataReader, T6>? m6 = null)
    {
        string key = BuildSchemaKey(reader, m1, m2, m3, m4, m5, m6);
        return Cache.GetOrAdd(key, _ => Create(reader, m1, m2, m3, m4, m5, m6));
    }

    private static string BuildSchemaKey(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3, Func<IDataReader, T4>? m4, Func<IDataReader, T5>? m5, Func<IDataReader, T6>? m6)
    {
        var parts = new string[reader.FieldCount + 7];
        parts[0] = reader.FieldCount.ToString();
        parts[1] = m1 is null ? "0" : "1";
        parts[2] = m2 is null ? "0" : "1";
        parts[3] = m3 is null ? "0" : "1";
        parts[4] = m4 is null ? "0" : "1";
        parts[5] = m5 is null ? "0" : "1";
        parts[6] = m6 is null ? "0" : "1";
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 7] = reader.GetName(i) ?? string.Empty;
        return string.Join("", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5, T6> Create(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3, Func<IDataReader, T4>? m4, Func<IDataReader, T5>? m5, Func<IDataReader, T6>? m6)
    {
        var c = new HashSet<int>();
        PropertySetter<T1>[] t1;
        if (m1 is null) { (t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c); foreach (int o in o1) c.Add(o); }
        else t1 = Array.Empty<PropertySetter<T1>>();
        PropertySetter<T2>[] t2;
        if (m2 is null) { (t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c); foreach (int o in o2) c.Add(o); }
        else t2 = Array.Empty<PropertySetter<T2>>();
        PropertySetter<T3>[] t3;
        if (m3 is null) { (t3, int[] o3) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c); foreach (int o in o3) c.Add(o); }
        else t3 = Array.Empty<PropertySetter<T3>>();
        PropertySetter<T4>[] t4;
        if (m4 is null) { (t4, int[] o4) = MultiEntityMapperCore.GetSettersExcluding<T4>(reader, c); foreach (int o in o4) c.Add(o); }
        else t4 = Array.Empty<PropertySetter<T4>>();
        PropertySetter<T5>[] t5;
        if (m5 is null) { (t5, int[] o5) = MultiEntityMapperCore.GetSettersExcluding<T5>(reader, c); foreach (int o in o5) c.Add(o); }
        else t5 = Array.Empty<PropertySetter<T5>>();
        PropertySetter<T6>[] t6;
        if (m6 is null) { (t6, _) = MultiEntityMapperCore.GetSettersExcluding<T6>(reader, c); }
        else t6 = Array.Empty<PropertySetter<T6>>();
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
    private static readonly ConcurrentDictionary<string, MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>> Cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly PropertySetter<T1>[] _t1;
    private readonly PropertySetter<T2>[] _t2;
    private readonly PropertySetter<T3>[] _t3;
    private readonly PropertySetter<T4>[] _t4;
    private readonly PropertySetter<T5>[] _t5;
    private readonly PropertySetter<T6>[] _t6;
    private readonly PropertySetter<T7>[] _t7;

    private MultiEntityMapper(PropertySetter<T1>[] t1, PropertySetter<T2>[] t2, PropertySetter<T3>[] t3, PropertySetter<T4>[] t4, PropertySetter<T5>[] t5, PropertySetter<T6>[] t6, PropertySetter<T7>[] t7)
    { _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; _t5 = t5; _t6 = t6; _t7 = t7; }

    public static MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> Build(
        IDataReader reader,
        Func<IDataReader, T1>? m1 = null,
        Func<IDataReader, T2>? m2 = null,
        Func<IDataReader, T3>? m3 = null,
        Func<IDataReader, T4>? m4 = null,
        Func<IDataReader, T5>? m5 = null,
        Func<IDataReader, T6>? m6 = null,
        Func<IDataReader, T7>? m7 = null)
    {
        string key = BuildSchemaKey(reader, m1, m2, m3, m4, m5, m6, m7);
        return Cache.GetOrAdd(key, _ => Create(reader, m1, m2, m3, m4, m5, m6, m7));
    }

    private static string BuildSchemaKey(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3, Func<IDataReader, T4>? m4, Func<IDataReader, T5>? m5, Func<IDataReader, T6>? m6, Func<IDataReader, T7>? m7)
    {
        var parts = new string[reader.FieldCount + 8];
        parts[0] = reader.FieldCount.ToString();
        parts[1] = m1 is null ? "0" : "1";
        parts[2] = m2 is null ? "0" : "1";
        parts[3] = m3 is null ? "0" : "1";
        parts[4] = m4 is null ? "0" : "1";
        parts[5] = m5 is null ? "0" : "1";
        parts[6] = m6 is null ? "0" : "1";
        parts[7] = m7 is null ? "0" : "1";
        for (int i = 0; i < reader.FieldCount; i++) parts[i + 8] = reader.GetName(i) ?? string.Empty;
        return string.Join("", parts);
    }

    private static MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7> Create(IDataReader reader, Func<IDataReader, T1>? m1, Func<IDataReader, T2>? m2, Func<IDataReader, T3>? m3, Func<IDataReader, T4>? m4, Func<IDataReader, T5>? m5, Func<IDataReader, T6>? m6, Func<IDataReader, T7>? m7)
    {
        var c = new HashSet<int>();
        PropertySetter<T1>[] t1;
        if (m1 is null) { (t1, int[] o1) = MultiEntityMapperCore.GetSettersExcluding<T1>(reader, c); foreach (int o in o1) c.Add(o); }
        else t1 = Array.Empty<PropertySetter<T1>>();
        PropertySetter<T2>[] t2;
        if (m2 is null) { (t2, int[] o2) = MultiEntityMapperCore.GetSettersExcluding<T2>(reader, c); foreach (int o in o2) c.Add(o); }
        else t2 = Array.Empty<PropertySetter<T2>>();
        PropertySetter<T3>[] t3;
        if (m3 is null) { (t3, int[] o3) = MultiEntityMapperCore.GetSettersExcluding<T3>(reader, c); foreach (int o in o3) c.Add(o); }
        else t3 = Array.Empty<PropertySetter<T3>>();
        PropertySetter<T4>[] t4;
        if (m4 is null) { (t4, int[] o4) = MultiEntityMapperCore.GetSettersExcluding<T4>(reader, c); foreach (int o in o4) c.Add(o); }
        else t4 = Array.Empty<PropertySetter<T4>>();
        PropertySetter<T5>[] t5;
        if (m5 is null) { (t5, int[] o5) = MultiEntityMapperCore.GetSettersExcluding<T5>(reader, c); foreach (int o in o5) c.Add(o); }
        else t5 = Array.Empty<PropertySetter<T5>>();
        PropertySetter<T6>[] t6;
        if (m6 is null) { (t6, int[] o6) = MultiEntityMapperCore.GetSettersExcluding<T6>(reader, c); foreach (int o in o6) c.Add(o); }
        else t6 = Array.Empty<PropertySetter<T6>>();
        PropertySetter<T7>[] t7;
        if (m7 is null) { (t7, _) = MultiEntityMapperCore.GetSettersExcluding<T7>(reader, c); }
        else t7 = Array.Empty<PropertySetter<T7>>();
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
