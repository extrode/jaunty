namespace Jaunty.Internals;

/// <summary>
/// Validates the <c>CommandTimeout</c> carried by <c>CommandOptions</c>, <c>CommandOptions&lt;T&gt;</c>
/// and the six <c>MultiEntityCommandOptions</c> arities.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R35-149. Nothing checked the value, so <c>WithTimeout(-1)</c> was accepted at the point the
/// caller wrote it and surfaced much later as a provider-specific exception from inside command
/// execution - <c>SqlCommand.CommandTimeout</c> throws <see cref="ArgumentException"/>, SQLite's
/// throws its own - with nothing naming the option that caused it.
/// </para>
/// <para>
/// This throws where the sibling <c>ExpectedRowCount</c> hint absorbs (see
/// <see cref="CapacityHint"/>), and the asymmetry is the same one recorded for
/// <c>BulkCopyConfiguration</c> under AUD-R35-144: a row-count hint only pre-sizes a list, so a
/// nonsensical value costs a reallocation and nothing else, while a timeout is an instruction to the
/// database about how long to wait. Silently substituting a different one is how a misconfiguration
/// survives to production looking as though it took effect.
/// </para>
/// <para>
/// Zero stays valid - ADO.NET reads it as "no timeout", which is a deliberate and dangerous choice a
/// caller is entitled to make.
/// </para>
/// </remarks>
internal static class CommandTimeoutHint
{
    /// <summary>
    /// Returns <paramref name="commandTimeout"/> unchanged, or throws if it is negative.
    /// </summary>
    internal static int? Require(int? commandTimeout)
    {
        if (commandTimeout is int seconds && seconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(commandTimeout),
                seconds,
                "The command timeout cannot be negative. Use 0 for no timeout, or null for the provider default.");
        }

        return commandTimeout;
    }
}
