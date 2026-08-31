namespace Jaunty.Internals;

/// <summary>
/// Common constants and cached values used throughout Jaunty.
/// </summary>
internal static class CommonConstants
{
    /// <summary>
    /// The one case-insensitive string comparer Jaunty keys its name lookups with - column names,
    /// parameter names and the reserved-name sets that guard them.
    /// </summary>
    /// <remarks>
    /// AUD-R35-113 (round-35 batch 04b, re-report of round 27). This used to say it "avoids
    /// allocating a new comparer instance per HashSet/Dictionary creation", which is false:
    /// <see cref="StringComparer.OrdinalIgnoreCase"/> is a cached BCL singleton and reading it
    /// allocates nothing. The real reason to name it once is policy, not allocation - fifteen call
    /// sites across <c>EntityMetadata</c>, <c>ParameterBinder</c> and <c>Upsert</c> have to agree on
    /// what "the same name" means, and going through one field makes changing that answer one edit
    /// rather than fifteen. Do not add a sibling field for a comparer on the allocation argument;
    /// add one only when a second policy genuinely needs a name.
    /// </remarks>
    public static readonly StringComparer OrdinalIgnoreCase = StringComparer.OrdinalIgnoreCase;
}