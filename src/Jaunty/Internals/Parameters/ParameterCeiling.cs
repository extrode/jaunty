using System.Data;

using Jaunty.Dialects;

namespace Jaunty.Internals.Parameters;

/// <summary>
/// The one place that decides whether a statement's parameter count exceeds what the engine will
/// accept, and the one place that words the resulting error.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26 (batch 5, medium/consistency). The ceiling used to be enforced on exactly one of four
/// routes to the same <c>IN</c> list. <see cref="ParameterBinder"/> checked collection expansion in
/// core, but Jaunty.Fluent expands collections itself into individually-named scalars and so never
/// reached that check. Measured on Microsoft.Data.Sqlite, all four spelling <c>Id IN (n values)</c>:
/// a 1,200-value list was rejected by <c>connection.Query("... IN @ids", new { ids })</c> with
/// "Consider batching the query into smaller chunks" and executed without comment by
/// <c>.Where(ids.Contains(p.Id))</c>, <c>.WhereIn(p =&gt; p.Id, ids)</c> and a hand-written
/// placeholder list. A caller who hit the core error and rewrote to the fluent form to work around
/// it got no error and no batching.
/// </para>
/// <para>
/// Sharing the check is only half of it - the ceiling it enforces has to be real. See
/// <see cref="SQLiteDialect.MaxParametersPerStatement"/> for the measurement that corrected
/// SQLite's from 999 to 32,766.
/// </para>
/// </remarks>
internal static class ParameterCeiling
{
    /// <summary>
    /// Throws when <paramref name="totalParameterCount"/> exceeds what
    /// <paramref name="dialect"/> reports the engine accepts in one statement.
    /// </summary>
    /// <param name="totalParameterCount">
    /// Parameters the statement will carry. Where a call site can only see part of the statement
    /// this may be a lower bound; erring low means a borderline statement reaches the provider
    /// rather than being refused by Jaunty, which is the safer direction for a guard whose failure
    /// mode used to be refusing queries the engine would have run.
    /// </param>
    /// <param name="dialect">The dialect whose ceiling applies.</param>
    /// <param name="providerDescription">
    /// What to name in the message - the connection type where a call site has one, so the reader
    /// can tell which provider imposed the limit.
    /// </param>
    public static void EnsureWithinLimit(int totalParameterCount, ISqlDialect dialect, string providerDescription)
    {
        int max = dialect.MaxParametersPerStatement;
        if (totalParameterCount <= max)
            return;

        throw new InvalidOperationException(
            $"Collection parameter expansion produces {totalParameterCount} parameters, exceeding the " +
            $"{providerDescription} provider's maximum of {max} parameters " +
            "per statement. Consider batching the query into smaller chunks.");
    }

    /// <summary>Names the provider in an error message, falling back to the dialect when there is no connection.</summary>
    public static string Describe(IDbConnection? connection, ISqlDialect dialect)
        => connection?.GetType().Name ?? dialect.GetType().Name;
}
