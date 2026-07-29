namespace Jaunty.Dialects;

/// <summary>
/// Optional companion to <see cref="ISqlDialect"/> for providers that cannot bind a
/// <see cref="decimal"/> as a number, and so cannot compare one against a SQL expression.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26 (batch 5, medium/bug). The joined and grouped fluent builders each coerced <b>every</b>
/// <see cref="decimal"/> parameter to <see cref="double"/> before binding, unconditionally and on
/// every dialect, attributing it to one provider: <i>"Some ADO.NET providers (observed with
/// System.Data.SQLite) don't correctly compare a bound decimal parameter against a REAL/numeric
/// column."</i> This interface is where that provider fact now lives, so it applies to the provider
/// it is about and to no other.
/// </para>
/// <para>
/// The measured behaviour, in raw ADO.NET on System.Data.SQLite 1.0.119:
/// <code>
/// SELECT typeof(@p)  bound 150m   -> text
/// SELECT typeof(@p)  bound 150.0d -> real
/// </code>
/// The provider binds a <see cref="decimal"/> as <b>TEXT</b>. Compared against a column with
/// numeric affinity, SQLite applies that affinity to the TEXT operand and converts it, so the
/// comparison works - which is why <c>WHERE price = @p</c> matches and why the coercion looked
/// unnecessary. A <i>bare</i> SQL expression has no affinity to apply, so no conversion happens and
/// SQLite's type ordering puts every TEXT value above every number:
/// <code>
/// HAVING SUM(price) &gt; @p   bound 150m   -> 0 rows      bound 150.0d -> 1 row
/// HAVING SUM(price) &lt; @p   bound 150m   -> every group bound 150.0d -> 1 group
/// WHERE  price * 1  &gt; @p   bound 100m   -> 0 rows      bound 100.0d -> 1 row
/// </code>
/// Not only aggregates: any computed operand. The comparison is not wrong by a rounding error, it
/// is decided by operand type before the numbers are looked at, silently and in both directions.
/// </para>
/// <para>
/// Both halves of that rule are narrower than "column good, expression bad", and the exceptions were
/// measured, not assumed. A column declared with <b>no</b> type, or with TEXT affinity, has no
/// numeric affinity to apply and fails exactly like a bare expression. Conversely an explicit
/// <c>CAST</c> carries affinity, so <c>CAST(SUM(price) AS REAL) &gt; @p</c> and
/// <c>SUM(price) &gt; CAST(@p AS NUMERIC)</c> both compare correctly with the TEXT-bound decimal.
/// The second of those is a <em>lossless</em> alternative to this interface - it would fix the
/// comparison without spending any precision - and is recorded as a round-27 candidate rather than
/// taken here, because it means changing generated SQL rather than a bound value.
/// </para>
/// <para>
/// The conversion costs precision - <see cref="double"/> carries 15-17 significant digits against
/// <see cref="decimal"/>'s 28-29 - so it is a trade, not a free fix, and it is the right trade only
/// where the alternative is a comparison that cannot be right at all. On SQL Server, PostgreSQL and
/// MySQL the provider binds a <see cref="decimal"/> as a decimal and the comparison is exact; those
/// dialects do not implement this interface, and a <c>DECIMAL(19,4)</c> or <c>NUMERIC</c> comparison
/// is no longer downgraded to binary floating point on their behalf.
/// </para>
/// <para>
/// A separate interface rather than a member on <see cref="ISqlDialect"/>, for the reason
/// <see cref="ISubstringToEndDialect"/> gives: that interface is public and this assembly targets
/// netstandard2.0, where a default interface implementation is not available to keep existing
/// external implementers compiling. Same drift hazard, too - a wrapping dialect that forgets to
/// re-declare it makes a provider that needs the conversion look like one that does not.
/// <c>DecimalBindingDialectTests</c> pins that.
/// </para>
/// <para>
/// That choice has one cost worth stating plainly. A dialect supplied by a caller through
/// <c>SqlDialectFactory.RegisterDialect</c> for a SQLite-backed connection - the documented route
/// for a wrapped or profiled connection - used to get the conversion, because the old coercion was
/// unconditional. It cannot implement an interface that did not exist when it was written, so it now
/// silently does not, and a HAVING comparison against a <see cref="decimal"/> goes from working to
/// quietly wrong for it. That population is narrow and the alternative was leaving three engines
/// returning wrong rows, but it is a real behaviour change and it is not detectable from here.
/// </para>
/// </remarks>
public interface IDecimalBindingDialect
{
    /// <summary>
    /// Converts a <see cref="decimal"/> into the CLR type this provider binds as a number.
    /// </summary>
    /// <param name="value">The value about to be bound.</param>
    /// <returns>The value to bind in its place.</returns>
    object ConvertDecimalParameter(decimal value);
}

/// <summary>
/// The single place that decides what a <see cref="decimal"/> parameter is bound as, including what
/// to do with a dialect that does not implement <see cref="IDecimalBindingDialect"/>.
/// </summary>
/// <remarks>
/// Every caller goes through here rather than writing its own <c>is</c> test, so the rule cannot
/// drift between the fluent builders and anything added later.
/// </remarks>
public static class DecimalParameterBinding
{
    /// <summary>
    /// Returns the value to bind for <paramref name="value"/> under <paramref name="dialect"/>.
    /// </summary>
    /// <param name="dialect">The dialect the command will run against.</param>
    /// <param name="value">The parameter value, which may be of any type or <see langword="null"/>.</param>
    /// <returns>
    /// The value unchanged, unless it is a <see cref="decimal"/> and the dialect asks for a
    /// conversion.
    /// </returns>
    public static object? Normalize(ISqlDialect dialect, object? value)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dialect);
#else
        if (dialect is null) throw new ArgumentNullException(nameof(dialect));
#endif

        return value is decimal d && dialect is IDecimalBindingDialect decimalDialect
            ? decimalDialect.ConvertDecimalParameter(d)
            : value;
    }
}
