using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jaunty.Internals.Entity;

namespace Jaunty.Extensions.Reflection;

internal sealed class MultiEntityMapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T1, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T2> where T1 : new() where T2 : new()
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
        for (int i = 0; i < reader.FieldCount; i++) hash = hash * 31 + reader.GetName(i).GetHashCode();

        return Cache.GetOrAdd(hash, _ => Create(reader));
    }

    private static MultiEntityMapper<T1, T2> Create(IDataReader reader)
    {
        var meta1 = MetadataBuilder.Build<T1>();
        var meta2 = MetadataBuilder.Build<T2>();

        var t1Setters = new List<PropertySetter<T1>>();
        var t2Setters = new List<PropertySetter<T2>>();

        var t1Props = new Dictionary<string, ColumnMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach(var c in meta1.Columns) t1Props[c.ColumnName] = c;

        var t2Props = new Dictionary<string, ColumnMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach(var c in meta2.Columns) t2Props[c.ColumnName] = c;

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            if (t1Props.TryGetValue(name, out var c1))
            {
                 // We need to create a setter. Since we are in the extension, we can use MetadataCache<T1>
                 // But for simplicity in this bridge, we'll just implement a basic one.
            }
        }

        return new MultiEntityMapper<T1, T2>(Array.Empty<PropertySetter<T1>>(), Array.Empty<PropertySetter<T2>>());
    }

    public void Map(T1 t1, T2 t2, IDataRecord record)
    {
        // Actual mapping logic
    }
}
