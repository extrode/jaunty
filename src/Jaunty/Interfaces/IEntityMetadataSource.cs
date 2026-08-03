using System.Collections.Generic;

namespace Jaunty.Interfaces;

/// <summary>
/// Represents an entity that can provide its own table/column metadata.
/// </summary>
/// <remarks>
/// <para>
/// Implemented by the Jaunty source generator on every <c>[Table]</c>-annotated partial class,
/// alongside <see cref="IMapped{T}"/>. Unlike <see cref="IMapped{T}"/>'s <c>static abstract</c>
/// member (net8.0+ only), this interface uses ordinary instance members so it can be resolved
/// uniformly on every target framework with no runtime reflection and no NativeAOT trim risk on
/// either side of the boundary.
/// </para>
/// <para>
/// AUD-R35-169. The check is <c>typeof(IEntityMetadataSource).IsAssignableFrom(typeof(T))</c>, made
/// <em>before</em> any <c>new T()</c>. This paragraph used to describe it as
/// <c>new T() is IEntityMetadataSource</c>, which AUD-R23-002 replaced precisely so that an entity
/// whose default constructor throws falls back to the reflection resolver instead of propagating.
/// A grep of <c>src/</c> for <c>is IEntityMetadataSource</c> returned this sentence and nothing
/// else.
/// </para>
/// <para>
/// Jaunty core converts the <see cref="EntityColumnInfo"/> values returned here into its internal
/// entity metadata representation; this interface's shape is intentionally independent of that
/// internal representation so it can evolve without becoming a breaking change.
/// </para>
/// </remarks>
public interface IEntityMetadataSource
{
    /// <summary>
    /// Gets the table name.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Gets the schema name, or <see langword="null"/> if not specified.
    /// </summary>
    string? SchemaName { get; }

    /// <summary>
    /// Gets the property-to-column mapping for every mapped property on the entity.
    /// </summary>
    IReadOnlyList<EntityColumnInfo> Columns { get; }
}
