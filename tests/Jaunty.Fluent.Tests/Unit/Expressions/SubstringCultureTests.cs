using System.Globalization;
using System.Linq.Expressions;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R26: <c>WhereExpressionVisitor</c>'s <c>Substring</c> translation formatted both the start
/// offset and the length with the ambient <see cref="CultureInfo.CurrentCulture"/> -
/// <c>sqlStart.ToString()</c> and <c>length?.ToString() ?? "1"</c>. Integers have no decimal or
/// group separator, so this looks harmless, but the <b>negative sign</b> is culture data: under the
/// Arabic cultures .NET prefixes it with an invisible bidi control character (U+061C ARABIC LETTER
/// MARK for ar-EG, U+200E LEFT-TO-RIGHT MARK for ar-MA), so <c>(-5).ToString()</c> is four chars,
/// not two. That character was emitted straight into the generated SQL text, where the database
/// rejects it as a syntax error - and because it renders as nothing, the error message shows what
/// looks like perfectly valid SQL.
///
/// <para>
/// A negative offset is reachable: the visitor <b>translates</b> the expression rather than running
/// it, so <c>p.ProductName.Substring(-3)</c> never hits the <see cref="ArgumentOutOfRangeException"/>
/// that calling it would raise, and yields <c>sqlStart = -2</c>.
/// </para>
/// </summary>
public class SubstringCultureTests
{
    private readonly TestDialect _dialect = new();

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

    private string TranslateInCulture(string culture, Expression<Func<Product, bool>> expr)
    {
        string sql = "";
        InCulture(culture, () => sql = new WhereExpressionVisitor<Product>(_dialect).Translate(expr).Sql);
        return sql;
    }

    [Theory]
    [InlineData("ar-EG")]
    [InlineData("ar-MA")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void NegativeStartOffset_EmitsAsciiHyphenOnly(string culture)
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(-3) == "x";

        string sql = TranslateInCulture(culture, expr);

        Assert.Contains("-2", sql, StringComparison.Ordinal);
        AssertNoBidiMarks(sql);
    }

    [Theory]
    [InlineData("ar-EG")]
    [InlineData("ar-MA")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void NegativeLength_EmitsAsciiHyphenOnly(string culture)
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(0, -5) == "x";

        string sql = TranslateInCulture(culture, expr);

        Assert.Contains("-5", sql, StringComparison.Ordinal);
        AssertNoBidiMarks(sql);
    }

    [Theory]
    [InlineData("ar-EG")]
    [InlineData("en-US")]
    public void PositiveArguments_AreUnaffected(string culture)
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(2, 4) == "x";

        string sql = TranslateInCulture(culture, expr);

        Assert.Contains("3", sql, StringComparison.Ordinal);
        Assert.Contains("4", sql, StringComparison.Ordinal);
        AssertNoBidiMarks(sql);
    }

    /// <summary>
    /// Guards the mechanism directly rather than the symptom: no invisible directionality control
    /// character may reach the SQL text, whatever the ambient culture decides a minus sign is.
    /// </summary>
    private static void AssertNoBidiMarks(string sql)
    {
        foreach (char c in sql)
        {
            Assert.False(
                c is '؜' or '‎' or '‏',
                $"Generated SQL contains bidi control character U+{(int)c:X4}: [{sql}]");
        }
    }

    /// <summary>
    /// The behaviour this test defends against is only interesting if .NET on this machine really
    /// does decorate the negative sign for these cultures - otherwise the tests above would pass
    /// with the bug still present. Skipped rather than failed under invariant globalization.
    /// </summary>
    [Fact]
    public void Precondition_ArabicCultureDecoratesNegativeSign()
    {
        var arabic = new CultureInfo("ar-EG");
        if (arabic.NumberFormat.NegativeSign == "-")
            return; // invariant globalization mode - nothing to guard against

        Assert.NotEqual("-5", (-5).ToString(arabic));
        Assert.Equal("-5", (-5).ToString(CultureInfo.InvariantCulture));
    }
}
