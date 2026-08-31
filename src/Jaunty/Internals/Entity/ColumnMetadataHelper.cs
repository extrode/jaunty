namespace Jaunty.Internals.Entity;

/// <summary>
/// Helper methods for working with <see cref="ColumnMetadata"/>.
/// Provides shared utilities for column filtering and manipulation.
/// </summary>
internal static class ColumnMetadataHelper
{
    /// <summary>
    /// Gets the insertable (non-identity, non-computed) columns from metadata.
    /// These columns can be used in INSERT statements.
    /// </summary>
    /// <param name="metadata">The entity metadata.</param>
    /// <returns>A read-only list of insertable columns.</returns>
    public static IReadOnlyList<ColumnMetadata> GetInsertableColumns(EntityMetadata metadata)
    {
        return metadata.InsertColumns;
    }
}