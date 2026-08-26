using System.Globalization;

using Jaunty.Extensions.Reflection;

using Xunit;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R25: the reflection mapper's terminal value conversion called
/// <c>Convert.ChangeType(value, targetType)</c> without an <see cref="IFormatProvider"/>, so it ran
/// under the ambient <see cref="CultureInfo.CurrentCulture"/>. Providers routinely hand back a
/// <see cref="string"/> where the column is TEXT/NUMERIC (SQLite in particular), and under a
/// comma-decimal culture the conversion does not throw - it reads the period as a group separator
/// and silently scales the value by a factor of ten.
///
/// <para>
/// The rest of the codebase already guarded this deliberately (<c>GridReader.ReadScalar</c>,
/// <c>GroupedJoinedResultMapper.ConvertColumnValue</c>, <c>HavingExpressionHelpers.FormatLiteral</c>);
/// these tests pin the behaviour for the sites that were left out.
/// </para>
/// </summary>
public class InvariantCultureConversionTests
{
    // Comma-decimal cultures: the period is a *group* separator, so "1.5" parses as 15 and
    // "1.234" as 1234 unless the conversion is pinned to the invariant culture.
    public static TheoryData<string> CommaDecimalCultures() =>
    [
        "de-DE",
        "fr-FR",
        "pt-BR",
        "it-IT",
    ];

    /// <summary>
    /// Runs <paramref name="body"/> with <see cref="CultureInfo.CurrentCulture"/> set to
    /// <paramref name="cultureName"/>, restoring it afterwards. Culture is per-async-flow in .NET,
    /// so this does not leak into concurrently running tests.
    /// </summary>
    private static void InCulture(string cultureName, Action body)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void InCulture_ActuallyChangesParsingBehaviour_SanityCheck()
    {
        // Guards the tests below: if the culture switch silently stopped taking effect (globalization
        // -invariant mode, for instance) every assertion here would pass vacuously.
        InCulture("de-DE", () =>
            Assert.Equal(15m, Convert.ChangeType("1.5", typeof(decimal), CultureInfo.CurrentCulture)));
    }

    // ------------------------------------------------------------------
    // DbValueConverter - terminal conversion for every reflection-mapped column read
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(CommaDecimalCultures))]
    public void DbValueConverter_StringToDecimal_IsCultureInvariant(string culture)
    {
        InCulture(culture, () =>
            Assert.Equal(1.5m, (decimal)DbValueConverter.ChangeType("1.5", typeof(decimal))));
    }

    [Theory]
    [MemberData(nameof(CommaDecimalCultures))]
    public void DbValueConverter_StringToDouble_IsCultureInvariant(string culture)
    {
        InCulture(culture, () =>
            Assert.Equal(1.5d, (double)DbValueConverter.ChangeType("1.5", typeof(double))));
    }

    [Theory]
    [MemberData(nameof(CommaDecimalCultures))]
    public void DbValueConverter_StringToNullableDecimal_IsCultureInvariant(string culture)
    {
        InCulture(culture, () =>
            Assert.Equal(1.5m, (decimal)DbValueConverter.ChangeType("1.5", typeof(decimal?))));
    }

    [Theory]
    [MemberData(nameof(CommaDecimalCultures))]
    public void DbValueConverter_StringWithGroupSeparatorSemantics_DoesNotSilentlyScale(string culture)
    {
        // The sharpest form of the bug: "1.234" is a perfectly valid de-DE integer-with-group-separator
        // spelling of 1234, so the conversion succeeds and returns the wrong number rather than throwing.
        InCulture(culture, () =>
            Assert.Equal(1.234m, (decimal)DbValueConverter.ChangeType("1.234", typeof(decimal))));
    }

    [Theory]
    [MemberData(nameof(CommaDecimalCultures))]
    public void DbValueConverter_StringToFloat_IsCultureInvariant(string culture)
    {
        InCulture(culture, () =>
            Assert.Equal(2.25f, (float)DbValueConverter.ChangeType("2.25", typeof(float))));
    }

    [Fact]
    public void DbValueConverter_NonNumericConversions_StillWork()
    {
        // The invariant-culture argument must not disturb the enum/Guid special cases around it.
        Assert.Equal(SampleEnum.Second, DbValueConverter.ChangeType("Second", typeof(SampleEnum)));
        Assert.Equal(SampleEnum.Second, DbValueConverter.ChangeType(1, typeof(SampleEnum)));
        Assert.Equal(Guid.Empty, DbValueConverter.ChangeType(Guid.Empty.ToString(), typeof(Guid)));
        Assert.Equal(42, DbValueConverter.ChangeType("42", typeof(int)));
        Assert.True((bool)DbValueConverter.ChangeType("true", typeof(bool)));
    }

    [Theory]
    [MemberData(nameof(CommaDecimalCultures))]
    public void DbValueConverter_NumericStringToEnum_IsCultureInvariant(string culture)
    {
        // Routes through the Enum.GetUnderlyingType branch, which had the same omission.
        InCulture(culture, () =>
            Assert.Equal(SampleEnum.Second, DbValueConverter.ChangeType("1", typeof(SampleEnum))));
    }

    private enum SampleEnum
    {
        First = 0,
        Second = 1,
    }
}
