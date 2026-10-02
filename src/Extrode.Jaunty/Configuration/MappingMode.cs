namespace Extrode.Jaunty.Configuration;

/// <summary>
/// Specifies the mapping mode for query result mapping.
/// </summary>
public enum MappingMode
{
    /// <summary>
    /// Columns and public writable properties must match one to one. Throws
    /// <see cref="InvalidOperationException"/> if a property has no matching column, or if the
    /// result set has a column that maps to no property.
    /// </summary>
    Strict,
    /// <summary>
    /// Only properties with matching columns are mapped. Properties with no column, and columns
    /// with no property, are both ignored.
    /// </summary>
    Projection
}