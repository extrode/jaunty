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
}