namespace Jaunty.Internals;

/// <summary>
/// Normalises the <c>ExpectedRowCount</c> pre-sizing hint carried by <c>CommandOptions</c> and the
/// <c>MultiEntityCommandOptions</c> family.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R35-122. The hint used to go straight into <c>new List&lt;T&gt;(capacity)</c> at every read
/// site - <c>GetAllCore</c>, <c>QueryCore</c>'s fifteen, <c>GridReader</c>'s two and the twelve
/// multi-entity ones - with nothing between. So <c>WithExpectedRowCount(-1)</c> failed inside the
/// <c>List</c> constructor with an <see cref="ArgumentOutOfRangeException"/> naming a parameter
/// called <c>capacity</c> that the caller never wrote, and a mistyped large value - a row count
/// pasted in as a byte count, say - threw <see cref="OutOfMemoryException"/> before a single row was
/// read.
/// </para>
/// <para>
/// The asymmetry is what settles which way to fix it. The fallback on the very same expression,
/// <c>JauntyConfig.QueryResultCapacity</c>, has a setter that clamps any non-positive value to 64:
/// the configuration layer already treats a bad capacity as something to absorb. A hint that can
/// abort the query is not a hint, so this absorbs it the same way - non-positive means "no hint
/// given", which falls back to the configured capacity, and anything absurd is capped rather than
/// allocated.
/// </para>
/// <para>
/// Normalising here rather than at the read sites keeps one answer for all thirty-odd of them, and
/// puts it where the value enters Jaunty instead of where it is consumed.
/// </para>
/// </remarks>
internal static class CapacityHint
{
    /// <summary>
    /// The largest pre-size honoured, chosen so a mistyped hint cannot allocate its way to
    /// <see cref="OutOfMemoryException"/> before the first row arrives.
    /// </summary>
    /// <remarks>
    /// A million-element <c>List&lt;T&gt;</c> of references is 8 MB on a 64-bit runtime, which is a
    /// real allocation but a survivable one; beyond that the list simply grows, which is the
    /// behaviour a caller who gave no hint at all already gets. Pre-sizing exists to avoid a handful
    /// of doublings, and the doublings above a million rows are not where a query's cost is.
    /// </remarks>
    internal const int MaxExpectedRowCount = 1_048_576;

    /// <summary>
    /// Clamps a caller-supplied row-count hint into the range the read paths can act on, or returns
    /// <see langword="null"/> when there is no usable hint.
    /// </summary>
    internal static int? Normalize(int? expectedRowCount)
    {
        if (expectedRowCount is not int hint || hint <= 0)
            return null;

        return hint > MaxExpectedRowCount ? MaxExpectedRowCount : hint;
    }
}
