namespace Jaunty.Internals;

/// <summary>
/// Common constants and cached values used throughout Jaunty.
/// </summary>
internal static class CommonConstants
{
    /// <summary>
    /// Cached StringComparer.OrdinalIgnoreCase instance.
    /// Using this avoids allocating a new comparer instance per HashSet/Dictionary creation.
    /// </summary>
    public static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Cached StringComparison.OrdinalIgnoreCase value.
    /// </summary>
    public static readonly StringComparison OrdinalIgnoreCaseComparison = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Default capacity for small collections (parameters, columns, etc.).
    /// Chosen to minimize reallocations while not wasting memory.
    /// </summary>
    public const int DefaultCollectionCapacity = 16;

    /// <summary>
    /// Default capacity for parameter lists in SQL parsing.
    /// Most queries have fewer than 8 parameters, so 4 is a good starting point.
    /// </summary>
    public const int InitialParameterCapacity = 4;

    /// <summary>
    /// Default capacity for multi-entity result lists.
    /// Large enough to avoid initial reallocations for typical queries.
    /// </summary>
    public const int DefaultResultCapacity = 64;
}