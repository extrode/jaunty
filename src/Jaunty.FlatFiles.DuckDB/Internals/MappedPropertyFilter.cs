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
