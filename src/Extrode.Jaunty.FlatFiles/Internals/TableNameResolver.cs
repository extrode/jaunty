using System.Reflection;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Internals.Entity;

namespace Extrode.Jaunty.FlatFiles.Internals;

/// <summary>
/// Resolves the logical table name for an entity type in the same order as core: a non-empty
/// <see cref="TableAttribute"/> name, else a non-empty DataAnnotations <c>[Table]</c> name, else
/// <c>JauntyConfig.TableNameResolver</c>, else the class name lowercased.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R35-243. This used to read the attributes only, so a configured
/// <c>JauntyConfig.TableNameResolver</c> named the table one way for core and another for
/// FlatFiles, and the same entity was unreachable from one side. <c>[Table("")]</c> returned the
/// empty name, which every registration and import then rejected as a zero-length identifier;
/// core skips an empty name, and so does this now. The order lives in core's
/// <see cref="NameResolution"/>; only the last resort differs, the lowercased class name FlatFiles
/// has always used.
/// </para>
/// <para>
/// The DataAnnotations attribute is read from the type itself, not from a base class, as core reads
/// it. This used to look through base classes (R28), so a derived entity took its base's table name
/// here and its own class name on core.
/// </para>
/// <para>
/// A schema is not applied: file views are registered in DuckDB's default schema, and an import
/// writes to the unqualified table name. See docs/06-releases/feature-candidates.md.
/// </para>
/// </remarks>
internal static class TableNameResolver
{
    private const string DataAnnotationsTableAttribute = "System.ComponentModel.DataAnnotations.Schema.TableAttribute";

    /// <summary>
    /// Resolves the table name for the specified entity type.
    /// </summary>
    public static string Resolve<T>() where T : class => Resolve(typeof(T));

    /// <summary>
    /// Resolves the table name for the specified entity type.
    /// </summary>
    public static string Resolve(Type entityType)
        => NameResolution.Table(entityType, AttributeName(entityType), entityType.Name.ToLowerInvariant());

    private static string? AttributeName(Type entityType)
    {
        // AOT-SAFE: attributes on a kept type survive trimming, and TableAttribute is rooted by the type argument.
        TableAttribute? jauntyAttr = entityType.GetCustomAttribute<TableAttribute>();
        if (jauntyAttr is not null)
            return jauntyAttr.Name;

        // AOT-SAFE: CustomAttributeData reads attribute metadata only; no member of the attribute type is accessed.
        foreach (CustomAttributeData attribute in entityType.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == DataAnnotationsTableAttribute)
                return attribute.ConstructorArguments.Count > 0 ? attribute.ConstructorArguments[0].Value as string : null;
        }

        return null;
    }
}
