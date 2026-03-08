namespace Jaunty.Internals.Enums;

/// <summary>
/// Specifies the mapping mode for query result mapping.
/// </summary>
public enum MappingMode
{
    /// <summary>
    /// All public writable properties must have matching columns in the result set.
    /// Throws <see cref="InvalidOperationException"/> if a column is missing.
    /// </summary>
    Strict,
    /// <summary>
    /// Only properties with matching columns are mapped. Missing columns are ignored.
    /// </summary>
    Projection
}