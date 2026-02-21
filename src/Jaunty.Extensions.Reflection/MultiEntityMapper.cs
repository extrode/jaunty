using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;

namespace Jaunty.Extensions.Reflection;

internal sealed class MultiEntityMapper<
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
    T1, 
#if NET5_0_OR_GREATER
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
    T2> where T1 : new() where T2 : new()
{
    private static readonly ConcurrentDictionary<int, MultiEntityMapper<T1, T2>> Cache = new();

    private readonly PropertySetter<T1>[] _t1Setters;
    private readonly PropertySetter<T2>[] _t2Setters;

    private MultiEntityMapper(PropertySetter<T1>[] t1Setters, PropertySetter<T2>[] t2Setters)
    {
        _t1Setters = t1Setters;
        _t2Setters = t2Setters;
    }

    public static MultiEntityMapper<T1, T2> Get(IDataReader reader)
    {
        // Simple hash for reader schema
        int hash = reader.FieldCount;
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            hash = (hash * 31) + (name?.GetHashCode() ?? 0);
        }

        return Cache.GetOrAdd(hash, _ => Create(reader));
    }

    /// <summary>Alias for Get — builds or retrieves a cached mapper for the reader schema.</summary>
    public static MultiEntityMapper<T1, T2> Build(IDataReader reader) => Get(reader);

    private static MultiEntityMapper<T1, T2> Create(IDataReader reader)
    {
        // Build setter arrays using MetadataCache for each type.
        // Using Projection mode to allow columns to be missing if they match the other type.
        var t1Setters = MetadataCache<T1>.GetSetters(reader, MappingMode.Projection);

        // T1 has priority: exclude from T2 any ordinals already claimed by T1.
        var t1Ordinals = new HashSet<int>();
        for (int i = 0; i < t1Setters.Length; i++)
            t1Ordinals.Add(t1Setters[i].Ordinal);

        var allT2Setters = MetadataCache<T2>.GetSetters(reader, MappingMode.Projection);
        var filteredT2 = new List<PropertySetter<T2>>(allT2Setters.Length);
        for (int i = 0; i < allT2Setters.Length; i++)
        {
            if (!t1Ordinals.Contains(allT2Setters[i].Ordinal))
                filteredT2.Add(allT2Setters[i]);
        }

        return new MultiEntityMapper<T1, T2>(t1Setters, filteredT2.ToArray());
    }

    public void Map(T1? t1, T2? t2, IDataRecord record)
    {
        if (t1 != null) ApplyT1(t1, record);
        if (t2 != null) ApplyT2(t2, record);
    }

    public void ApplyT1(T1 target, IDataRecord record)
    {
        for (int i = 0; i < _t1Setters.Length; i++)
            _t1Setters[i].Set(target, record);
    }

    public void ApplyT2(T2 target, IDataRecord record)
    {
        for (int i = 0; i < _t2Setters.Length; i++)
            _t2Setters[i].Set(target, record);
    }
}
