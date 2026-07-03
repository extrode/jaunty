using System;
using System.Collections.Generic;
using System.Data;

using Jaunty.Internals.Enums;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Shared ordinal-claiming algorithm used by all N-ary MultiEntityMapper variants.
/// </summary>
/// <remarks>
/// Types are processed left-to-right: T1 claims ordinals first, T2 claims from the remainder,
/// T3 from the remainder after T1+T2, and so on. Types with a custom per-position mapper
/// delegate are excluded from ordinal claiming entirely (they don't claim, and other types
/// can freely claim those columns).
/// </remarks>
internal static class MultiEntityMapperCore
{
    /// <summary>
    /// Builds property setter arrays for a sequence of types, applying left-to-right
    /// ordinal claiming. For each type, ordinals already claimed by earlier types are excluded.
    /// </summary>
    /// <param name="reader">The data reader to resolve column ordinals from.</param>
    /// <param name="typeGetters">
    /// One entry per type position. Each entry is a factory that, given the current
    /// claimed-ordinal set, returns (setterArray, ordinalsClaimedByThisType).
    /// A null entry means this position uses a custom mapper and participates in neither
    /// claiming nor exclusion.
    /// </param>
    /// <returns>The ordered list of raw setter arrays (one per type, null for custom-mapped positions).</returns>
    public static Array?[] BuildSetterArrays(
        IDataReader reader,
        Func<HashSet<int>, (Array setters, int[] ordinalsClaimed)>?[] typeGetters)
    {
        var claimed = new HashSet<int>();
        var result = new Array?[typeGetters.Length];

        for (int i = 0; i < typeGetters.Length; i++)
        {
            var getter = typeGetters[i];
            if (getter is null)
            {
                result[i] = null; // custom mapper position
                continue;
            }

            (Array setters, int[] ordinalsClaimed) = getter(claimed);
            result[i] = setters;

            for (int j = 0; j < ordinalsClaimed.Length; j++)
                claimed.Add(ordinalsClaimed[j]);
        }

        return result;
    }

    /// <summary>
    /// Produces the getter factory for a specific type T given the current claimed set.
    /// Returns setters excluding already-claimed ordinals, and lists the ordinals this
    /// type claims.
    /// </summary>
    public static (PropertySetter<T>[], int[]) GetSettersExcluding<T>(
        IDataReader reader,
        HashSet<int> alreadyClaimed)
        where T : new()
    {
        PropertySetter<T>[] allSetters = MetadataCache<T>.GetSetters(reader, MappingMode.Projection);

        var filtered = new List<PropertySetter<T>>(allSetters.Length);
        var ordinals = new List<int>(allSetters.Length);

        for (int i = 0; i < allSetters.Length; i++)
        {
            int ord = allSetters[i].Ordinal;
            if (!alreadyClaimed.Contains(ord))
            {
                filtered.Add(allSetters[i]);
                ordinals.Add(ord);
            }
        }

        return (filtered.ToArray(), ordinals.ToArray());
    }
}
