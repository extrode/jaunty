using System.Runtime.CompilerServices;

using DuckDB.NET.Data;

using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Records, per connection, which sources were registered as preloaded in-memory TABLEs rather
/// than as VIEWs.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R35-026, and the same shape as AUD-R26-069's fix for <c>IsPromotedToTable</c>. Preloading is
/// a decision <em>one connection</em> made at registration time, but it was stored as
/// <see cref="IFileSource.IsPreloaded"/> - settable state on an object the caller owns and may
/// share. <c>DuckDb</c>'s constructor wrote it and never cleared it, so once one
/// <c>FlatFileOptions</c> with <c>PreloadIntoMemory = true</c> had registered a source instance,
/// that same instance registered into a second <c>FlatFileOptions</c> with
/// <c>PreloadIntoMemory = false</c> still emitted <c>CREATE OR REPLACE TABLE</c> instead of a VIEW,
/// still skipped the promoted-table guard in <c>RegisterSource</c>, and still short-circuited
/// <c>TablePromoter.EnsurePromotedToTable</c> - so a mutation on the second connection ran against
/// a view. AUD-R26-069 deliberately left <c>IsPreloaded</c> as the short-circuit, which made it the
/// one remaining connection-scoped decision read off a shareable source.
/// </para>
/// <para>
/// <see cref="IFileSource.IsPreloaded"/> keeps its documented meaning as the caller's per-source
/// opt-in (<c>FlatFileOptions.PreloadIntoMemory</c>'s "individual sources can override this"), and
/// is read once at registration to seed this table. It is no longer written by the constructor, so
/// nothing a connection does to it can leak into another one.
/// </para>
/// </remarks>
internal static class PreloadRegistry
{
    private static readonly ConditionalWeakTable<DuckDBConnection, HashSet<IFileSource>> States = new();

    /// <summary>Records that <paramref name="source"/> is preloaded on <paramref name="connection"/>.</summary>
    /// <param name="connection">The connection the source was registered on.</param>
    /// <param name="source">The source registered as a TABLE.</param>
    public static void Mark(DuckDBConnection connection, IFileSource source)
    {
        HashSet<IFileSource> preloaded = States.GetValue(connection, static _ => new HashSet<IFileSource>());

        lock (preloaded)
            preloaded.Add(source);
    }

    /// <summary>Whether <paramref name="source"/> was registered as preloaded on <paramref name="connection"/>.</summary>
    /// <param name="connection">The connection to ask about.</param>
    /// <param name="source">The source to test.</param>
    public static bool IsPreloaded(DuckDBConnection connection, IFileSource source)
    {
        if (!States.TryGetValue(connection, out HashSet<IFileSource>? preloaded))
            return false;

        lock (preloaded)
            return preloaded.Contains(source);
    }
}
