using System.Reflection;

using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Decides whether a property participates in flat-file column mapping.
/// </summary>
/// <remarks>
/// AUD-R25: <see cref="ColumnMappingCache"/> and
/// <see cref="Import.TargetDdlGenerator"/> each grew their own property loop and neither honoured
/// <see cref="IgnoreAttribute"/> or <c>[NotMapped]</c>, even though both mappers that feed the same
/// entity types do - <c>MetadataBuilder</c> for the reflection path and <c>JauntyGenerator</c> for
/// the source-generated one. The result was an entity whose <c>[Ignore]</c>d property was excluded
/// from every core CRUD statement but included in flat-file reads, exports and generated import
/// DDL. Both call sites now share this predicate so the rule cannot drift again.
/// </remarks>
internal static class MappedPropertyFilter
{
    // Matched by full name rather than referenced: System.ComponentModel.DataAnnotations.Schema is
    // not a dependency of this package, and MetadataBuilder resolves it the same way.
    private const string NotMappedAttributeTypeName = "System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute";

    /// <summary>
    /// Returns the mapped properties of <paramref name="entityType"/>, with hidden base declarations
    /// removed.
    /// </summary>
    /// <remarks>
    /// AUD-R26: <c>GetProperties(Public | Instance)</c> returns BOTH declarations when a <c>new</c>
    /// shadow changes the property type - measured: <c>string Code</c> hiding <c>object Code</c>
    /// yields two <see cref="PropertyInfo"/>s, where a same-type shadow collapses to one because
    /// reflection hides by name <em>and</em> signature. Those two are one logical property, so
    /// everything downstream must see only the most-derived declaration.
    ///
    /// <para>
    /// Resolved by comparing <see cref="MemberInfo.DeclaringType"/> rather than by relying on
    /// enumeration order. <c>GetProperties</c> has listed the most-derived declaration first in every
    /// CoreCLR release, but the documentation explicitly does not guarantee order - and if it ever
    /// flipped, the hidden base declaration would silently win, giving the wrong property type in
    /// both the mapping and the generated DDL with no diagnostic.
    /// </para>
    ///
    /// <para>
    /// A single type cannot declare two same-name non-indexer properties, and indexers are filtered
    /// out below, so two same-name entries in one enumeration can only ever be a hide chain.
    /// </para>
    /// </remarks>
    /// <param name="entityType">The entity to enumerate.</param>
    /// <returns>The mapped properties, one per logical property.</returns>
    public static List<PropertyInfo> GetMappedProperties(Type entityType)
    {
        // AOT-SAFE: FlatFiles.DuckDB maps by reflection by design and is on no AOT publish path - no NativeAOT sample or the Scaffolding CLI references it. A trimmed consumer fails loudly at first map, not silently.
        PropertyInfo[] all = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var byName = new Dictionary<string, int>(all.Length, StringComparer.Ordinal);
        var candidates = new List<PropertyInfo>(all.Length);

        // AUD-R35-028: the hide chain is collapsed FIRST and IsMapped applied to the survivor. The
        // other order dropped the derived declaration and then admitted the base one, so
        //
        //     class Base    { public object Code { get; set; } }
        //     class Derived : Base { [Ignore] public new string Code { get; set; } }
        //
        // mapped Base.Code as a column - the exact opposite of what the attribute asks for, and
        // with the wrong CLR type on top. Same for [NotMapped], and for a `new` declaration that
        // makes the property read-only over a read-write base. Only the most-derived declaration is
        // the one C# binds, so it is the only one whose attributes have any say.
        for (int i = 0; i < all.Length; i++)
        {
            PropertyInfo property = all[i];

            // Indexers are excluded before the name dictionary rather than by IsMapped below,
            // because several of them share the name "Item" without being a hide chain.
            if (property.GetIndexParameters().Length > 0) continue;

            if (byName.TryGetValue(property.Name, out int existingIndex))
            {
                if (IsMoreDerivedThan(property, candidates[existingIndex]))
                    candidates[existingIndex] = property;

                continue;
            }

            byName[property.Name] = candidates.Count;
            candidates.Add(property);
        }

        var result = new List<PropertyInfo>(candidates.Count);

        for (int i = 0; i < candidates.Count; i++)
        {
            if (IsMapped(candidates[i]))
                result.Add(candidates[i]);
        }

        return result;
    }

    private static bool IsMoreDerivedThan(PropertyInfo candidate, PropertyInfo incumbent)
    {
        Type? candidateType = candidate.DeclaringType;
        Type? incumbentType = incumbent.DeclaringType;

        if (candidateType is null || incumbentType is null || candidateType == incumbentType)
            return false;

        // candidate is more derived when the incumbent's declaring type is one of its bases.
        return incumbentType.IsAssignableFrom(candidateType);
    }

    /// <summary>
    /// The column a mapped property maps to: its <c>[Column]</c> name, or its own name.
    /// </summary>
    /// <remarks>
    /// AUD-R26-067: this two-line rule existed in three places -
    /// <see cref="ColumnMappingCache"/>, <see cref="Import.TargetDdlGenerator"/>'s inline resolution
    /// and <c>ExpressionTranslator.GetColumnName</c>. The first two had already been brought under
    /// this class's <see cref="GetMappedProperties"/> for the *filtering* half of the rule while
    /// each keeping its own copy of the *naming* half; the third was outside both. Same class of
    /// drift AUD-R25 created this type for, so the naming half lives here now as well.
    /// <para>
    /// AUD-R35-071: the null-coalesce alone is not the rule. <see cref="ColumnAttribute"/>'s
    /// constructor rejects null but not <c>""</c>, so <c>[Column("")]</c> mapped the property to the
    /// empty column name - the read path then never matched a file column and left the property
    /// silently unset, and <c>TargetDdlGenerator</c> emitted an empty-named column into the
    /// generated import DDL. AUD-R32-006 added the <c>IsNullOrEmpty</c> guard to
    /// <c>MetadataBuilder</c> and the source generator carries it too; this was the third path,
    /// which did not.
    /// </para>
    /// </remarks>
    public static string GetColumnName(PropertyInfo property)
    {
        string? name = property.GetCustomAttribute<ColumnAttribute>()?.Name;
        return string.IsNullOrEmpty(name) ? property.Name : name!;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="property"/> should be treated as a column.
    /// </summary>
    public static bool IsMapped(PropertyInfo property)
    {
        // An indexer ("public object this[int i]") surfaces as a public instance property named
        // "Item" with index parameters, and both call sites go on to build an Expression.Property
        // or read it as a column - which throws. MetadataBuilder and ParameterCache already skip
        // these (R16/AUD-R22); these two loops did not.
        if (property.GetIndexParameters().Length > 0) return false;

        if (!property.CanRead || !property.CanWrite) return false;

        if (property.GetCustomAttribute<IgnoreAttribute>() is not null) return false;

        foreach (CustomAttributeData attribute in property.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == NotMappedAttributeTypeName) return false;
        }

        return true;
    }
}
