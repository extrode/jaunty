namespace Jaunty.Dialects;

/// <summary>
/// Optional companion to <see cref="ISqlDialect"/> for engines whose <c>AVG</c> takes its result
/// type from its operand, and so truncates the average of an integer column to an integer.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R35-066. <c>IGrouping.Avg&lt;TResult&gt;</c> and its joined siblings are declared to return
/// <see cref="double"/>, but both translators emitted a bare <c>AVG(&lt;column&gt;)</c> with no
/// cast. On SQL Server, <c>AVG</c> over an <c>int</c> column returns an <c>int</c>: the average is
/// truncated in the engine, then widened to <see cref="double"/> by the mapper, so a caller
/// receives <c>12.0</c> where the true average is <c>12.6</c>. The declared <see cref="double"/>
/// promises the fractional result on every dialect.
/// </para>
/// <para>
/// SQLite (always float), MySQL (decimal) and PostgreSQL (numeric) do not truncate, which is why a
/// green suite never showed it - and why they do not implement this interface. A dialect that does
/// not implement it is not broken; callers fall back to a bare <c>AVG</c>, which is already
/// fractional there.
/// </para>
/// <para>
/// Deliberately a separate interface rather than a member on <see cref="ISqlDialect"/>, for the
/// reason spelled out on <see cref="ISubstringToEndDialect"/>: that interface is public and this
/// assembly targets netstandard2.0, where a default interface implementation is not available to
/// keep existing external implementers compiling.
/// </para>
/// </remarks>
public interface IFractionalAverageDialect
{
    /// <summary>
    /// Generates an <c>AVG</c> whose result is fractional regardless of the operand's type.
    /// </summary>
    /// <param name="operand">The already-escaped column or expression to average.</param>
    /// <returns>The dialect's fractional <c>AVG</c> expression.</returns>
    string GenerateFractionalAverage(string operand);
}

/// <summary>
/// The single place that decides what a fractional <c>AVG</c> compiles to, including what to do
/// with a dialect that does not implement <see cref="IFractionalAverageDialect"/>.
/// </summary>
/// <remarks>
/// Every caller goes through here rather than writing its own <c>is</c> test, so the fallback rule
/// cannot drift between the two GROUP BY expression visitors and the bulk-copy dialect wrappers -
/// which is exactly how the un-joined and joined translators came to hold the same defect twice.
/// </remarks>
public static class FractionalAverage
{
    /// <summary>
    /// Generates an <c>AVG</c> whose result is fractional, using the dialect's own form where it
    /// needs one and a bare <c>AVG</c> where it does not.
    /// </summary>
    /// <param name="dialect">The dialect to generate for.</param>
    /// <param name="operand">The already-escaped column or expression to average.</param>
    /// <returns>The <c>AVG</c> expression.</returns>
    public static string Generate(ISqlDialect dialect, string operand)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dialect);
#else
        if (dialect is null) throw new ArgumentNullException(nameof(dialect));
#endif

        return dialect is IFractionalAverageDialect fractional
            ? fractional.GenerateFractionalAverage(operand)
            : $"AVG({operand})";
    }
}
