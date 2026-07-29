namespace Jaunty.Dialects;

/// <summary>
/// Optional companion to <see cref="ISqlDialect"/> for "the rest of the string" - the
/// single-argument <c>string.Substring(int)</c>, which has no length to pass on.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISqlDialect.GenerateSubstring"/> requires a length, so a translator with no length
/// to give had to invent one. It used the literal <c>8000</c> - a SQL Server convention, in a
/// dialect-neutral expression visitor, applied to every engine. Measured against a 10,000-character
/// value it silently returns 8,000 characters on both SQLite and DuckDB: a predicate written to
/// mean "everything from here on" quietly stops meaning that past the 8,000th character.
/// </para>
/// <para>
/// Every engine but SQL Server has a native two-argument form that says exactly this and needs no
/// sentinel at all. SQL Server has no such form, so it - and only it - supplies a length, and it
/// supplies one that cannot truncate. That is the point of routing this through the dialect: the
/// SQL Server number now lives in <c>SqlServerDialect</c>, where a SQL Server number belongs.
/// </para>
/// <para>
/// This is deliberately a separate interface rather than a member on <see cref="ISqlDialect"/>:
/// that interface is public and this assembly targets netstandard2.0, where a default interface
/// implementation is not available to keep existing external implementers compiling. This mirrors
/// <c>IQuotedIdentifierDialect</c> in <c>Jaunty.FlatFiles</c>, added for the same reason.
/// A dialect that does not implement this still works - callers fall back to
/// <see cref="ISqlDialect.GenerateSubstring"/> with a length large enough not to truncate.
/// </para>
/// <para>
/// Because the compiler cannot enforce an <em>optional</em> interface the way it enforces a new
/// <see cref="ISqlDialect"/> member, every shipping dialect implementing one and not the other is
/// a silent drift hazard. <c>SubstringToEndDialectTests</c> exists to catch that.
/// </para>
/// </remarks>
public interface ISubstringToEndDialect
{
    /// <summary>
    /// Generates SQL for a substring running from <paramref name="start"/> to the end of the value.
    /// </summary>
    /// <param name="expression">The SQL expression for the string.</param>
    /// <param name="start">The start position SQL expression (1-based).</param>
    /// <returns>Dialect-specific substring SQL expression, with no upper bound on the result length.</returns>
    string GenerateSubstringToEnd(string expression, string start);
}

/// <summary>
/// The single place that decides what "the rest of the string" compiles to, including what to do
/// with a dialect that does not implement <see cref="ISubstringToEndDialect"/>.
/// </summary>
/// <remarks>
/// Every caller goes through here rather than writing its own <c>is</c> test, so the fallback rule
/// cannot drift between the expression visitors and the bulk-copy dialect wrappers.
/// </remarks>
public static class SubstringToEnd
{
    /// <summary>
    /// The length handed to <see cref="ISqlDialect.GenerateSubstring"/> for a dialect that does not
    /// implement <see cref="ISubstringToEndDialect"/>. <c>int.MaxValue</c>: a sentinel is
    /// unavoidable there, but it should be one no real value can exceed rather than one - like the
    /// 8000 this replaced - that silently truncates.
    /// </summary>
    public const string FallbackLength = "2147483647";

    /// <summary>
    /// Generates SQL for a substring running from <paramref name="start"/> to the end of the value,
    /// using the dialect's native form where it has one.
    /// </summary>
    /// <param name="dialect">The dialect to generate for.</param>
    /// <param name="expression">The SQL expression for the string.</param>
    /// <param name="start">The start position SQL expression (1-based).</param>
    /// <returns>Dialect-specific substring SQL expression.</returns>
    public static string Generate(ISqlDialect dialect, string expression, string start)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dialect);
#else
        if (dialect is null) throw new ArgumentNullException(nameof(dialect));
#endif

        return dialect is ISubstringToEndDialect toEnd
            ? toEnd.GenerateSubstringToEnd(expression, start)
            : dialect.GenerateSubstring(expression, start, FallbackLength);
    }
}
