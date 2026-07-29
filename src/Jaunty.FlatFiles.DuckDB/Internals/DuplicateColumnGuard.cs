using System.Reflection;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Rejects an entity that maps two properties onto the same column name.
/// </summary>
/// <remarks>
/// AUD-R26: the three code paths that resolve columns from an entity all disagreed about this.
/// Measured with <c>[Column("code")]</c> on one property and <c>[Column("CODE")]</c> on another:
/// <list type="bullet">
/// <item><description>
/// <see cref="ColumnMappingCache"/> kept <b>one</b> mapping. Its dictionary is
/// <see cref="StringComparer.OrdinalIgnoreCase"/> and it assigned with <c>dict[name] = ...</c>, so
/// the two collapsed and the <b>last</b> won. The other property was silently never read and never
/// written - no exception, no diagnostic, just absent data.
/// </description></item>
/// <item><description>
/// <c>TargetDdlGenerator.GetColumnDefinitions</c> kept <b>both</b>, producing
/// <c>CREATE TABLE ... ("code" TEXT, "CODE" TEXT)</c>. SQLite rejects that outright
/// (<c>duplicate column name: CODE</c>) and so does SQL Server, whose identifiers are
/// case-insensitive under the default collation - so <c>CreateTableIfMissing</c> failed the import
/// before a single row moved, with an error naming neither property.
/// </description></item>
/// <item><description>
/// <c>JauntyGenerator</c> already diagnoses it at compile time as <c>JAUNTYGEN001</c> - and keeps
/// the <b>first</b>, the opposite of <see cref="ColumnMappingCache"/>.
/// </description></item>
/// </list>
///
/// <para>
/// On PostgreSQL the case-differing variant was worse than an error: that dialect quotes
/// identifiers, so <c>"code"</c> and <c>"CODE"</c> are genuinely distinct columns there. The DDL
/// succeeded, the table got two columns, and the mapper only ever populated one - silent partial
/// data rather than a failure.
/// </para>
///
/// <para>
/// Both runtime paths now call this before building anything, so the ambiguity is reported once,
/// early, naming both properties - rather than resolved three different ways. Throwing rather than
/// picking a winner is deliberate: every available winner is a guess about which property the caller
/// meant, and two of the three targets already refused the DDL that the silent choice produced.
/// <see cref="MappedPropertyFilter"/> exists (AUD-R25) because these same two loops had already
/// drifted once on <c>[Ignore]</c>/<c>[NotMapped]</c>; this is the same shared rule.
/// </para>
/// </remarks>
internal static class DuplicateColumnGuard
{
    /// <summary>
    /// Throws when two mapped properties resolve to the same column name, compared
    /// case-insensitively because the target databases compare identifiers that way.
    /// </summary>
    /// <remarks>
    /// Hidden base declarations never reach here: <see cref="MappedPropertyFilter.GetMappedProperties"/>
    /// collapses each hide chain to its most-derived declaration first, so two entries claiming one
    /// column are always two genuinely distinct properties.
    /// </remarks>
    /// <param name="entityType">The entity being mapped, for the error message.</param>
    /// <param name="columnName">The column name just resolved.</param>
    /// <param name="property">The property that resolved to it.</param>
    /// <param name="seen">Accumulator of column names already claimed, and by which property.</param>
    /// <exception cref="InvalidOperationException">Thrown on the second claim of a column name.</exception>
    public static void Claim(
        Type entityType,
        string columnName,
        PropertyInfo property,
        Dictionary<string, string> seen)
    {
        if (seen.TryGetValue(columnName, out string? firstProperty))
        {
            throw new InvalidOperationException(
                $"Entity '{entityType.Name}' maps more than one property to column '{columnName}': " +
                $"'{firstProperty}' and '{property.Name}'. Column names are compared " +
                $"case-insensitively because the target databases compare identifiers that way. " +
                $"Give one of them a distinct [Column(\"...\")] name, or mark it [Ignore].");
        }

        seen[columnName] = property.Name;
    }

    /// <summary>
    /// Creates the accumulator <see cref="Claim"/> expects.
    /// </summary>
    /// <param name="capacity">Expected number of columns.</param>
    /// <returns>An empty, case-insensitive accumulator.</returns>
    public static Dictionary<string, string> NewClaimSet(int capacity)
        => new(capacity, StringComparer.OrdinalIgnoreCase);
}
