using Extrode.Jaunty.Configuration;

namespace Extrode.Jaunty.Internals.Entity;

/// <summary>
/// The one order every mapping mode resolves a table, schema or column name in: a non-empty
/// attribute name, then the <see cref="JauntyConfig"/> resolver when it is set and returns
/// non-null, then the C# name. Shared by the reflection <c>MetadataBuilder</c> and the
/// source-generated <see cref="Core.GeneratedNameCache{TState}"/>, so the two cannot drift.
/// </summary>
/// <remarks>
/// <para>
/// docs/plans/2026-08-29-005 carried "naming resolvers are ignored on the source-generated path"
/// as an owner decision for three rounds: the generator baked attribute-or-C# names into the
/// mapper, binders and metadata at build time, so a source-generated entity ignored all three
/// resolvers while a non-strict <c>QueryPartial</c> read of the same entity, which goes through
/// reflection, applied the column resolver. The owner ruled that the generated path follows the
/// reflection order; docs/02-architecture/metadata-system-spec.md diagrams it.
/// </para>
/// <para>
/// A resolver is consulted only when no attribute name applies. A resolver returning
/// <see langword="null"/> falls through to the C# name; one returning <c>""</c> is used as is.
/// </para>
/// </remarks>
internal static class NameResolution
{
    /// <summary>The table name: <paramref name="attributeName"/> if non-empty, else <c>TableNameResolver</c>, else the type's name.</summary>
    public static string Table(Type entityType, string? attributeName)
    {
        if (!string.IsNullOrEmpty(attributeName))
            return attributeName!;

        return JauntyConfig.TableNameResolver?.Invoke(entityType) ?? entityType.Name;
    }

    /// <summary>The schema name: <paramref name="attributeSchema"/> if non-empty, else <c>SchemaNameResolver</c>, else none.</summary>
    public static string? Schema(Type entityType, string? attributeSchema)
    {
        if (!string.IsNullOrEmpty(attributeSchema))
            return attributeSchema;

        return JauntyConfig.SchemaNameResolver?.Invoke(entityType);
    }

    /// <summary>The column name: <paramref name="attributeName"/> if non-empty, else <c>ColumnNameResolver</c>, else the property's name.</summary>
    public static string Column(string propertyName, string? attributeName)
    {
        if (!string.IsNullOrEmpty(attributeName))
            return attributeName!;

        return JauntyConfig.ColumnNameResolver?.Invoke(propertyName) ?? propertyName;
    }
}
