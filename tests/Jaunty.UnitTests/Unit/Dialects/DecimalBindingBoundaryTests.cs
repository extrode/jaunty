using System.Reflection;

using Jaunty.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// Boundary companion to <see cref="DecimalBindingDialectTests"/>, which pins <em>which</em>
/// dialects convert a <see cref="decimal"/> and <see cref="DecimalBindingBoundaryTests"/> pins
/// <em>what the conversion costs</em>.
///
/// <para>
/// <c>SQLiteDialect.ConvertDecimalParameter</c> binds a decimal as a <see cref="double"/>, and its
/// remarks state the price precisely: <i>"a value past double's 15-17 significant digits is no
/// longer bound exactly, so an exact-INTEGER comparison past 2^53 that used to match now does not -
/// (double)9007199254740993m is 9007199254740992"</i>. That is a documented, deliberate,
/// one-directional loss on a shipped conversion, and until this file nothing asserted it. A
/// documented trade with no test is a trade that can silently become a different trade.
/// </para>
///
/// <para>
/// These are the handover's §3 "exact boundary values from compound numeric constraints" written
/// by hand rather than solved. The constraint surface here is small enough to enumerate in one
/// sitting - which is exactly the condition under which the handover's own gate says not to reach
/// for a solver - and the output would have been these same checked-in cases either way.
/// </para>
/// </summary>
public class DecimalBindingBoundaryTests
{
    /// <summary>2^53. The largest integer for which every integer below is exactly representable.</summary>
    private const decimal DoubleExactIntegerCeiling = 9007199254740992m;

    /// <summary>
    /// Every integer up to and including 2^53 survives the conversion unchanged, so the
    /// overwhelming majority of real key and money values are unaffected by the TEXT-binding fix.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(16777217L)]              // 2^24 + 1 - the first integer a float cannot represent,
                                         // so this case fails on any narrowing of the conversion
                                         // that a power-of-two boundary would sail through
    [InlineData(4503599627370496L)]      // 2^52
    [InlineData(9007199254740991L)]      // 2^53 - 1
    [InlineData(9007199254740992L)]      // 2^53
    [InlineData(-9007199254740992L)]     // -2^53
    public void AnIntegerWithinDoublesExactRangeSurvivesTheConversion(long value)
    {
        foreach (ISqlDialect dialect in ConvertingDialects())
        {
            object? bound = DecimalParameterBinding.Normalize(dialect, (decimal)value);

            Assert.IsType<double>(bound);
            Assert.Equal((double)value, (double)bound!);
            Assert.Equal(value, (long)(double)bound!);
        }
    }

    /// <summary>
    /// The asymmetry that makes the forward conversion easy to misread. <c>decimal</c> to
    /// <c>double</c> is exact for every integer up to 2^53, but <c>double</c> back to
    /// <c>decimal</c> rounds to <b>15 significant digits</b> - so 2^52 survives being bound and
    /// still reads back as 4503599627370500 if anyone round-trips it. Measured, not assumed: the
    /// first draft of the fact above asserted the round-trip and failed on exactly these values.
    ///
    /// <para>
    /// It matters because the loss a caller sees is not necessarily the loss the binding caused.
    /// Anyone diagnosing a mismatched <c>HAVING</c> comparison by casting back to decimal will
    /// measure this rounding on top of the real effect.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(4503599627370496L, "4503599627370500")]   // 2^52
    [InlineData(9007199254740992L, "9007199254740990")]   // 2^53
    public void TheReverseConversionRoundsToFifteenSignificantDigits(long value, string readBack)
    {
        foreach (ISqlDialect dialect in ConvertingDialects())
        {
            double bound = (double)DecimalParameterBinding.Normalize(dialect, (decimal)value)!;

            Assert.Equal((double)value, bound);
            Assert.Equal(decimal.Parse(readBack, System.Globalization.CultureInfo.InvariantCulture),
                         (decimal)bound);
        }
    }

    /// <summary>
    /// 2^53 + 1 is the first integer that does not survive, and it lands on 2^53. This is the exact
    /// case the XML remarks name; if the conversion is ever changed, this fact is what says so.
    /// </summary>
    [Fact]
    public void TheFirstIntegerPastDoublesExactRangeCollapsesOntoIt()
    {
        const decimal justPast = 9007199254740993m; // 2^53 + 1

        foreach (ISqlDialect dialect in ConvertingDialects())
        {
            object? bound = DecimalParameterBinding.Normalize(dialect, justPast);

            Assert.IsType<double>(bound);
            Assert.Equal((double)DoubleExactIntegerCeiling, (double)bound!);
            Assert.NotEqual(justPast, (decimal)(double)bound!);
        }
    }

    /// <summary>
    /// Two decimals that differ only past double's significand collide after conversion. This is
    /// the shape of the failure a caller actually sees: a <c>HAVING</c> comparison that should
    /// distinguish two values stops being able to.
    /// </summary>
    [Fact]
    public void TwoDecimalsDifferingPastTheSignificandBecomeIndistinguishable()
    {
        const decimal a = 1.00000000000000000000001m;
        const decimal b = 1.00000000000000000000002m;

        Assert.NotEqual(a, b);

        foreach (ISqlDialect dialect in ConvertingDialects())
        {
            double boundA = (double)DecimalParameterBinding.Normalize(dialect, a)!;
            double boundB = (double)DecimalParameterBinding.Normalize(dialect, b)!;

            Assert.Equal(boundA, boundB);
        }
    }

    /// <summary>
    /// The conversion is total: no decimal in range, including both extremes and the smallest
    /// non-zero value, makes it throw or produce a non-finite double. A cast that threw here would
    /// surface as an exception from an ordinary query.
    /// </summary>
    [Theory]
    [InlineData("79228162514264337593543950335")]   // decimal.MaxValue
    [InlineData("-79228162514264337593543950335")]  // decimal.MinValue
    [InlineData("0.0000000000000000000000000001")]  // decimal.Epsilon-equivalent
    [InlineData("-0.0000000000000000000000000001")]
    [InlineData("0.1")]
    [InlineData("-0.5")]
    public void TheConversionIsTotalAndFiniteAcrossDecimalsRange(string literal)
    {
        decimal value = decimal.Parse(literal, System.Globalization.CultureInfo.InvariantCulture);

        foreach (ISqlDialect dialect in ConvertingDialects())
        {
            object? bound = DecimalParameterBinding.Normalize(dialect, value);

            Assert.IsType<double>(bound);

            double d = (double)bound!;
            Assert.False(double.IsNaN(d), $"{literal} produced NaN.");
            Assert.False(double.IsInfinity(d), $"{literal} produced an infinity.");
        }
    }

    /// <summary>
    /// A money value nowhere near a range limit is still approximated: 0.1m has no exact double,
    /// so the bound value is the nearest one and accumulates. The 15-significant-digit round-trip
    /// hides this - <c>(decimal)(double)0.1m</c> is 0.1m again - which is why the assertion is on
    /// arithmetic over bound values rather than on a single round-trip.
    /// </summary>
    [Fact]
    public void ATenthIsApproximatedRatherThanRepresented()
    {
        foreach (ISqlDialect dialect in ConvertingDialects())
        {
            double tenth = (double)DecimalParameterBinding.Normalize(dialect, 0.1m)!;
            double fifth = (double)DecimalParameterBinding.Normalize(dialect, 0.2m)!;
            double third = (double)DecimalParameterBinding.Normalize(dialect, 0.3m)!;

            Assert.Equal(0.3m, 0.1m + 0.2m);
            Assert.NotEqual(third, tenth + fifth);
        }
    }

    /// <summary>
    /// At least one dialect must reach these assertions. Every other fact here loops over a
    /// discovered set, so an interface rename or a dropped implementation would leave them
    /// iterating nothing and passing.
    /// </summary>
    [Fact]
    public void AtLeastOneShippingDialectConvertsDecimals()
    {
        Assert.NotEmpty(ConvertingDialects());
    }

    private static List<ISqlDialect> ConvertingDialects()
    {
        Assembly[] shipping =
        [
            typeof(ISqlDialect).Assembly,
            typeof(global::Jaunty.Extensions.Reflection.JauntyReflectionExtensions).Assembly,
        ];

        List<ISqlDialect> dialects = [];

        foreach (Assembly assembly in shipping)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(ISqlDialect).IsAssignableFrom(type))
                    continue;

                if (!typeof(IDecimalBindingDialect).IsAssignableFrom(type))
                    continue;

                ConstructorInfo? ctor = type
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .OrderBy(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (ctor is null)
                    continue;

                object?[] args = [.. ctor.GetParameters()
                    .Select(p => p.HasDefaultValue ? p.DefaultValue : null)];

                dialects.Add((ISqlDialect)ctor.Invoke(args));
            }
        }

        return dialects;
    }
}
