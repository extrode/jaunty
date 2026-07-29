using System.Globalization;

using Jaunty.StoredProcedure;

using Xunit;

namespace Jaunty.Tests.Unit.StoredProcedures;

/// <summary>
/// AUD-R26 (batch 2, low/bug). <c>SpParameters.GetReturnValue()</c> converted with
/// <c>Convert.ToInt32(value)</c>, whose no-provider overload runs under
/// <see cref="CultureInfo.CurrentCulture"/>. <c>Get&lt;T&gt;(name)</c> two methods above it reads
/// the same <c>param.DbParameter?.Value ?? param.Value</c> expression and routes through
/// <c>ScalarConverter&lt;T&gt;</c>, which pins <see cref="CultureInfo.InvariantCulture"/> on all
/// seven of its conversion paths - so two accessors for the same provider value disagreed on
/// culture, and <c>GetReturnValue</c> was the host-locale-dependent one.
///
/// <para>
/// Measured, not assumed: under <c>ar-SA</c> and <c>fa-IR</c>, <c>Convert.ToInt32("-42")</c> throws
/// <see cref="FormatException"/> while the invariant form returns <c>-42</c>, because those cultures
/// do not use ASCII <c>-</c> as their negative sign. A stored procedure whose provider surfaces its
/// return value as a string would therefore have worked on an English host and thrown on an Arabic
/// or Persian one.
/// </para>
///
/// <para>
/// Same defect class as the round-25 sweep (AUD-R25-002/-003/-004), which fixed six such sites so
/// "the classes could not drift apart again". This one was missed because that sweep matched
/// <c>Convert.ChangeType</c> and this site was <c>Convert.ToInt32</c>.
/// </para>
/// </summary>
public class GetReturnValueCultureTests
{
    /// <summary>The cultures the divergence was measured under.</summary>
    private static readonly string[] HostileCultures = ["ar-SA", "fa-IR", "de-DE", "sv-SE"];

    private static SpParameters WithReturnValue(object? value)
    {
        var parameters = new SpParameters().AddReturnValue();

        // The provider writes back through SpParameter.Value after execution; setting it directly is
        // the same state a completed stored procedure leaves behind, without needing a live server
        // that supports RETURN.
        parameters.Parameters[0].GetType()
            .GetProperty(nameof(SpParameter.Value))!
            .SetValue(parameters.Parameters[0], value);

        return parameters;
    }

    private static T UnderCulture<T>(string cultureName, Func<T> body)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            return body();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// The measured reproduction. Before the fix this threw <see cref="FormatException"/> on the
    /// cultures whose negative sign is not ASCII <c>-</c>.
    /// </summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("fa-IR")]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    [InlineData("en-US")]
    public void ANegativeStringReturnValue_ParsesUnderEveryCulture(string culture)
        => Assert.Equal(-42, UnderCulture(culture, () => WithReturnValue("-42").GetReturnValue()));

    /// <summary>
    /// The control. If the runtime is in invariant-globalization mode every culture collapses to the
    /// invariant one and the theory above proves nothing, so this asserts the divergence is real on
    /// this host before the assertions that depend on it are believed.
    /// </summary>
    [Fact]
    public void TheCultureDivergence_IsRealOnThisHost()
    {
        bool anyDiverges = HostileCultures.Any(name => UnderCulture(name, () =>
        {
            try
            {
                return Convert.ToInt32("-42") != -42;
            }
            catch (FormatException)
            {
                return true;
            }
        }));

        Assert.True(anyDiverges,
            "No culture on this host makes Convert.ToInt32(\"-42\") diverge from the invariant result - " +
            "the runtime is probably in invariant-globalization mode, and these tests are not exercising " +
            "what they claim to.");
    }

    [Theory]
    [InlineData("ar-SA")]
    [InlineData("fa-IR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void APositiveStringReturnValue_ParsesUnderEveryCulture(string culture)
        => Assert.Equal(42, UnderCulture(culture, () => WithReturnValue("42").GetReturnValue()));

    /// <summary>
    /// The two accessors read the same underlying value and must now agree, which is the point of
    /// routing both through <c>ScalarConverter</c> rather than giving <c>GetReturnValue</c> its own
    /// invariant argument.
    /// </summary>
    [Theory]
    [InlineData("ar-SA")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void GetReturnValue_AgreesWithGetOfInt(string culture)
    {
        SpParameters parameters = WithReturnValue("-42");

        (int viaReturnValue, int viaGet) = UnderCulture(culture,
            () => (parameters.GetReturnValue(), parameters.Get<int>("RETURN_VALUE")));

        Assert.Equal(viaGet, viaReturnValue);
        Assert.Equal(-42, viaReturnValue);
    }

    // ------------------------------------------------------------------
    // What must not change
    // ------------------------------------------------------------------

    [Fact]
    public void AnIntegerReturnValue_IsUnchanged()
        => Assert.Equal(7, WithReturnValue(7).GetReturnValue());

    [Fact]
    public void ANullReturnValue_IsStillZero()
        => Assert.Equal(0, WithReturnValue(null).GetReturnValue());

    [Fact]
    public void ADBNullReturnValue_IsStillZero()
        => Assert.Equal(0, WithReturnValue(DBNull.Value).GetReturnValue());

    [Fact]
    public void NoReturnValueParameter_StillThrows()
    {
        var parameters = new SpParameters().AddInput("CategoryId", 5);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => parameters.GetReturnValue());

        Assert.Contains("AddReturnValue()", ex.Message, StringComparison.Ordinal);
    }
}
