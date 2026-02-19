using System.Collections.Concurrent;
using System.Data;
using System.Text;

using Jaunty.Internals.Entity;

namespace Jaunty;

/// <summary>
/// Builds and caches column-to-property mappings for two entity types.
/// Uses property-name matching: each column is mapped to the first type that has a matching property.
/// </summary>
internal sealed class MultiEntityMapper<T1, T2> where T1 : new() where T2 : new()
{
    private static readonly ConcurrentDictionary<string, MultiEntityMapper<T1, T2>> Cache = new(StringComparer.Ordinal);

    private readonly PropertySetter<T1>[] _t1Setters;
    private readonly PropertySetter<T2>[] _t2Setters;

    private MultiEntityMapper(PropertySetter<T1>[] t1Setters, PropertySetter<T2>[] t2Setters)
    {
        _t1Setters = t1Setters;
        _t2Setters = t2Setters;
    }

    internal static MultiEntityMapper<T1, T2> Build(IDataReader reader)
    {
        string signature = GetReaderSignature(reader);
        return Cache.GetOrAdd(signature, _ => CreateMapper(reader));
    }

    private static string GetReaderSignature(IDataReader reader)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append(reader.GetName(i));
        }
        return sb.ToString();
    }

    private static MultiEntityMapper<T1, T2> CreateMapper(IDataReader reader)
    {
        var t1Props = new Dictionary<string, PropertyContext<T1>>(MetadataCache<T1>.Properties.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var ctx in MetadataCache<T1>.Properties)
        {
            t1Props[ctx.ColumnName] = ctx;
            if (!ctx.ColumnName.Equals(ctx.PropertyName, StringComparison.OrdinalIgnoreCase))
                t1Props[ctx.PropertyName] = ctx;
        }

        var t2Props = new Dictionary<string, PropertyContext<T2>>(MetadataCache<T2>.Properties.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var ctx in MetadataCache<T2>.Properties)
        {
            t2Props[ctx.ColumnName] = ctx;
            if (!ctx.ColumnName.Equals(ctx.PropertyName, StringComparison.OrdinalIgnoreCase))
                t2Props[ctx.PropertyName] = ctx;
        }

        var t1Setters = new List<PropertySetter<T1>>();
        var t2Setters = new List<PropertySetter<T2>>();

        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);

            // T1 has priority
            if (t1Props.TryGetValue(columnName, out var t1Ctx))
            {
                t1Setters.Add(new PropertySetter<T1>(t1Ctx, i));
                continue;
            }

            // Then T2
            if (t2Props.TryGetValue(columnName, out var t2Ctx))
            {
                t2Setters.Add(new PropertySetter<T2>(t2Ctx, i));
            }
        }

        return new MultiEntityMapper<T1, T2>([.. t1Setters], [.. t2Setters]);
    }

    internal void ApplyT1(T1 target, IDataRecord record)
    {
#if NET8_0_OR_GREATER
        ReadOnlySpan<PropertySetter<T1>> localSetters = _t1Setters;
        foreach (ref readonly var setter in localSetters)
            setter.Set(target, record);
#else
        for (int i = 0; i < _t1Setters.Length; i++)
            _t1Setters[i].Set(target, record);
#endif
    }

    internal void ApplyT2(T2 target, IDataRecord record)
    {
#if NET8_0_OR_GREATER
        ReadOnlySpan<PropertySetter<T2>> localSetters = _t2Setters;
        foreach (ref readonly var setter in localSetters)
            setter.Set(target, record);
#else
        for (int i = 0; i < _t2Setters.Length; i++)
            _t2Setters[i].Set(target, record);
#endif
    }
}
