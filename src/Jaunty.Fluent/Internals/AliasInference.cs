using System.Collections.Generic;

using Jaunty.Dialects;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Derives a table alias from the parameter name the caller wrote in a join lambda, so
/// <c>On(p =&gt; p.CategoryId, c =&gt; c.CategoryId)</c> emits <c>FROM products p INNER JOIN
/// categories c</c> rather than qualifying every column with its full table name.
/// </summary>
/// <remarks>
/// <para>
/// The name is available because Roslyn lowers a lambda assigned to an <see cref="System.Linq.Expressions.Expression{TDelegate}"/>
/// into <c>Expression.Parameter(typeof(Product), "p")</c>, where <c>"p"</c> is a string literal in
/// the IL of the factory call. It is tree data rather than metadata about the entity, so trimming
/// and NativeAOT cannot remove it and reading it costs a property access, not reflection.
/// </para>
/// <para>
/// A name never decides which <em>table</em> a column belongs to - the join visitors bind by
/// <see cref="System.Linq.Expressions.ParameterExpression"/> reference identity, and arities 3 and
/// 4 pass their aliases positionally. Inference chooses the qualifier text and nothing else. The
/// hazard it does have to guard is alias capture: an inferred alias equal to a real table name in
/// the same query turns <c>FROM products categories JOIN categories c</c> into SQL where every
/// <c>categories.</c> reference means products.
/// </para>
/// <para>
/// Inference is all-or-nothing per join step. If any name a step needs is unusable, that step
/// keeps the full table names it emits today, so nothing that compiles now begins to throw and no
/// query ends up half-aliased in a way that reads as a mistake. The one place a fallback is not
/// available is a self-join with neither side aliased, where two occurrences of the same table
/// name are ambiguous SQL; that case takes the positional <c>t1</c>/<c>t2</c> scheme instead.
/// </para>
/// <para>
/// Candidates are validated against <see cref="SqlIdentifierFlavor.Common"/> rather than the
/// dialect's own flavour. Common is the intersection every engine accepts, it covers every C#
/// identifier a caller can write, and a rejection here costs only the verbose form - so the
/// stricter set is the right one for a name the caller never intended as SQL.
/// </para>
/// </remarks>
internal static class AliasInference
{
    /// <summary>
    /// Aliases for a two-table join, or the aliases unchanged when either name is unusable.
    /// </summary>
    /// <param name="dialect">Supplies the keyword set an alias must avoid.</param>
    /// <param name="fromAlias">The caller's explicit FROM alias, or null to infer one.</param>
    /// <param name="joinAlias">The caller's explicit join alias, or null to infer one.</param>
    /// <param name="fromTable">The FROM table's name, which an alias may not shadow.</param>
    /// <param name="joinTable">The joined table's name, which an alias may not shadow.</param>
    /// <param name="fromName">The lambda parameter naming the FROM entity, or null.</param>
    /// <param name="joinName">The lambda parameter naming the joined entity, or null.</param>
    internal static (string? From, string? Join) ForJoin(
        ISqlDialect dialect,
        string? fromAlias,
        string? joinAlias,
        string fromTable,
        string joinTable,
        string? fromName,
        string? joinName)
    {
        bool selfJoin = Same(fromTable, joinTable);

        if (fromAlias is not null && joinAlias is not null)
            return (fromAlias, joinAlias);

        var taken = new List<string>(4) { fromTable, joinTable };
        if (fromAlias is not null) taken.Add(fromAlias);
        if (joinAlias is not null) taken.Add(joinAlias);

        string? inferredFrom = fromAlias;
        if (inferredFrom is null)
        {
            inferredFrom = Sanction(dialect, fromName, taken);
            if (inferredFrom is null)
                return Fallback(fromAlias, joinAlias, selfJoin);

            taken.Add(inferredFrom);
        }

        string? inferredJoin = joinAlias;
        if (inferredJoin is null)
        {
            inferredJoin = Sanction(dialect, joinName, taken);
            if (inferredJoin is null)
                return Fallback(fromAlias, joinAlias, selfJoin);
        }

        return (inferredFrom, inferredJoin);
    }

    /// <summary>
    /// The alias for a table entering the query at a later join, given what earlier joins locked in.
    /// </summary>
    /// <remarks>
    /// Earlier joins have already rendered their ON conditions, so their aliases cannot be revised
    /// and this step decides one slot against them. A step that cannot infer leaves the new table
    /// unaliased, which is correct SQL as long as its name is still free; when the name is already
    /// taken - a third table repeating one of the first two - the positional scheme applies for the
    /// same reason it does for a two-table self-join.
    /// </remarks>
    /// <param name="dialect">Supplies the keyword set an alias must avoid.</param>
    /// <param name="aliases">Aliases already locked in by earlier joins; nulls are unaliased tables.</param>
    /// <param name="tables">Table names already in the query, none of which an alias may shadow.</param>
    /// <param name="newTable">The table this join introduces.</param>
    /// <param name="newName">The lambda parameter naming the new entity, or null.</param>
    /// <param name="slot">Zero-based position of the new table, used by the positional fallback.</param>
    internal static string? ForAddedJoin(
        ISqlDialect dialect,
        IReadOnlyList<string?> aliases,
        IReadOnlyList<string> tables,
        string newTable,
        string? newName,
        int slot)
    {
        var taken = new List<string>(tables.Count + aliases.Count + 1);

        for (var i = 0; i < tables.Count; i++)
            taken.Add(tables[i]);

        for (var i = 0; i < aliases.Count; i++)
        {
            if (aliases[i] is { } alias)
                taken.Add(alias);
        }

        string? inferred = Sanction(dialect, newName, taken);

        // A candidate equal to the table it would alias is rejected rather than emitted: `JOIN
        // suppliers suppliers` is legal but says nothing, and the unaliased form is what the
        // caller's own column references already expect. It cannot go in `taken`, because that
        // list is also the positional fallback's test for whether the bare name is still free.
        if (inferred is not null && !Same(inferred, newTable))
            return inferred;

        return Contains(taken, newTable) ? Positional(taken, slot) : null;
    }

    /// <summary>
    /// The candidate if it can be emitted verbatim as a bare alias, otherwise null.
    /// </summary>
    /// <remarks>
    /// Aliases are written unquoted, so a keyword cannot be accepted and escaped the way
    /// <see cref="ISqlDialect.EscapeColumnName"/> escapes a column. The uniqueness test is
    /// case-insensitive because SQL Server folds case, and two lambdas naming their parameters
    /// <c>p</c> and <c>P</c> would collide there while remaining distinct in C#.
    /// </remarks>
    private static string? Sanction(ISqlDialect dialect, string? candidate, List<string> taken)
    {
        if (string.IsNullOrEmpty(candidate))
            return null;

        if (!SqlIdentifierValidator.IsValid(candidate, SqlIdentifierFlavor.Common))
            return null;

        if (dialect.IsKeyword(candidate!))
            return null;

        return Contains(taken, candidate!) ? null : candidate;
    }

    private static (string? From, string? Join) Fallback(string? fromAlias, string? joinAlias, bool selfJoin)
    {
        if (!selfJoin)
            return (fromAlias, joinAlias);

        // Both sides unaliased against one table renders `categories.x = categories.y`, which no
        // engine can resolve. Only the pair needs the scheme; one explicit alias is enough to make
        // the other occurrence's bare table name unambiguous.
        if (fromAlias is null && joinAlias is null)
            return (Positional(0), Positional(1));

        return (fromAlias, joinAlias);
    }

    private static string Positional(int slot) => "t" + (slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The first positional name at or after <paramref name="slot"/> that nothing in the query has
    /// already claimed.
    /// </summary>
    /// <remarks>
    /// The slot number alone is not safe: a caller who writes <c>From&lt;Order&gt;("t3")</c>, or a
    /// lambda parameter named <c>t3</c> in an earlier join, owns that name before this step runs,
    /// and the earlier ON is a rendered string that cannot be revised. Emitting it twice is not a
    /// worse alias but invalid SQL - "the correlation name 't3' is specified multiple times" - so
    /// the scheme skips forward instead. It terminates because <paramref name="taken"/> is fixed
    /// here and each candidate is distinct, so one outside it is found within taken.Count + 1 steps.
    /// </remarks>
    private static string Positional(List<string> taken, int slot)
    {
        string candidate = Positional(slot);

        for (var next = slot + 1; Contains(taken, candidate); next++)
            candidate = Positional(next);

        return candidate;
    }

    private static bool Contains(List<string> taken, string candidate)
    {
        for (var i = 0; i < taken.Count; i++)
        {
            if (Same(taken[i], candidate))
                return true;
        }

        return false;
    }

    private static bool Same(string left, string right)
        => string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
}
