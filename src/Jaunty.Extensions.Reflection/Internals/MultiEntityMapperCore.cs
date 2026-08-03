using System;
using System.Collections.Generic;
using System.Data;

using Jaunty.Configuration;

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
        // MetadataCache<T>.GetSetters binds each property to the FIRST column in the
        // reader whose name matches, regardless of what other types have claimed.
        // When that first-bound ordinal was already claimed by an earlier type, we
        // must not simply drop the property — we must rebind it to the next
        // still-unclaimed column with the same name (the documented left-to-right
        // ordinal-claiming behavior), so duplicate column names (e.g. "id" appearing
        // once per joined table) are still claimed correctly by later types.
        PropertySetter<T>[] allSetters = MetadataCache<T>.GetSetters(reader, MappingMode.Projection);

        var result = new List<PropertySetter<T>>(allSetters.Length);
        var claimedByThisType = new HashSet<int>();

        for (int i = 0; i < allSetters.Length; i++)
        {
            PropertySetter<T> setter = allSetters[i];
            int ord = setter.Ordinal;

            if (!alreadyClaimed.Contains(ord) && !claimedByThisType.Contains(ord))
            {
                result.Add(setter);
                claimedByThisType.Add(ord);
                continue;
            }

            int replacement = FindNextUnclaimedOrdinal<T>(reader, setter.Context, alreadyClaimed, claimedByThisType);
            if (replacement >= 0)
            {
                result.Add(new PropertySetter<T>(setter.Context, replacement));
                claimedByThisType.Add(replacement);
            }
            // Otherwise there is no remaining unclaimed column with this name; the
            // property is left unmapped, consistent with MappingMode.Projection.
        }

        var ordinals = new int[result.Count];
        for (int i = 0; i < result.Count; i++)
            ordinals[i] = result[i].Ordinal;

        return (result.ToArray(), ordinals);
    }

    /// <summary>
    /// Finds the leftmost still-unclaimed reader column that <c>MetadataCache&lt;T&gt;.BuildSetters</c>
    /// would itself have bound to <paramref name="context"/>'s property.
    /// </summary>
    /// <remarks>
    /// AUD-R35-022. This used to compare the reader's column name against
    /// <c>Context.ColumnName</c> alone, but <c>BuildSetters</c> binds on three names, not one: the
    /// metadata column name, the property-name fallback alias registered in the second pass of
    /// <c>Snapshot</c>'s constructor (so <c>Id [Column("ProductID")]</c> binds a reader column
    /// literally named <c>Id</c>), and the <c>ColumnNameResolver</c> index - though that last one
    /// is a second route to a name <c>MetadataBuilder</c> has already resolved, so only the alias
    /// diverges in practice. A setter first bound through that alias, whose ordinal an earlier
    /// type had already claimed, was
    /// searched for under a name no column in the reader carries - so the rebind found nothing and
    /// the property was silently left unmapped, which is exactly the case the left-to-right
    /// claiming exists to handle. Asking the metadata the same question it asked when binding
    /// cannot drift from it.
    /// </remarks>
    private static int FindNextUnclaimedOrdinal<T>(
        IDataReader reader,
        in PropertyContext<T> context,
        HashSet<int> alreadyClaimed,
        HashSet<int> claimedByThisType)
        where T : new()
    {
        for (int ord = 0; ord < reader.FieldCount; ord++)
        {
            if (alreadyClaimed.Contains(ord) || claimedByThisType.Contains(ord))
                continue;

            string? candidateName = reader.GetName(ord);
            if (candidateName is not null && MetadataCache<T>.ColumnBindsTo(candidateName, context))
                return ord;
        }

        return -1;
    }
}
