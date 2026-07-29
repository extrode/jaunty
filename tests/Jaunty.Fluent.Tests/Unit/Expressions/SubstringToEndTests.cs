using System.Linq.Expressions;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Tests.Entities;
using Jaunty.Fluent.Tests.Helpers;

using Xunit;

namespace Jaunty.Fluent.Tests.Unit.Expressions;

/// <summary>
/// AUD-R26 (the unresolved half of the <c>Substring</c> finding, carried from the round-26 close-out
/// as PARTIALLY FIXED). The single-argument <c>string.Substring(int)</c> means "the rest of the
/// string", and the visitor had no length to pass to
/// <see cref="ISqlDialect.GenerateSubstring"/> - so it invented one: the literal <c>8000</c>, a SQL
/// Server convention, emitted from a dialect-neutral expression visitor and applied to every engine.
///
/// <para>
/// It truncates. Measured against a 10,000-character value, <c>SUBSTR(s, 2, 8000)</c> returns 8,000
/// characters on SQLite and <c>SUBSTRING(s, 2, 8000)</c> returns 8,000 on DuckDB, where the native
/// two-argument form returns all 9,999. A predicate written to mean "everything from here on"
/// quietly stopped meaning that past the 8,000th character - silently, with no error to notice.
/// </para>
///
/// <para>
/// Every engine but SQL Server can say this natively and needs no sentinel at all, so the decision
/// belongs to the dialect. <see cref="ISubstringToEndDialect"/> is the optional interface that
/// carries it; SQL Server, which has no two-argument SUBSTRING, is the one dialect that still names
/// a length - and now names one that cannot truncate, from inside the SQL Server dialect where a
/// SQL Server number belongs.
/// </para>
/// </summary>
public class SubstringToEndTests
{
    private static string Translate(ISqlDialect dialect, Expression<Func<Product, bool>> expr)
        => new WhereExpressionVisitor<Product>(dialect).Translate(expr).Sql;

    // ------------------------------------------------------------------
    // The native form, per engine
    // ------------------------------------------------------------------

    public static TheoryData<string, string> NativeForms => new()
    {
        // PostgreSQL: omitting FOR is the ANSI "to the end" form.
        { "postgres", "SUBSTRING(product_name FROM 4)" },
        // MySQL and SQLite: two-argument call, no length.
        { "mysql", "SUBSTRING(product_name, 4)" },
        { "sqlite", "SUBSTR(product_name, 4)" },
        // SQL Server has no two-argument form, so it alone still supplies a length.
        { "sqlserver", "SUBSTRING(product_name, 4, 2147483647)" },
    };

    private static ISqlDialect DialectFor(string name) => name switch
    {
        "postgres" => new PostgreSqlDialect(),
        "mysql" => new MySqlDialect(),
        "sqlite" => new SQLiteDialect(),
        "sqlserver" => new SqlServerDialect(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown dialect"),
    };

    [Theory]
    [MemberData(nameof(NativeForms))]
    public void SubstringWithoutLength_UsesTheDialectsOwnForm(string dialectName, string expected)
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(3) == "t";

        string sql = Translate(DialectFor(dialectName), expr);

        Assert.Contains(expected, sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// The defect itself, stated directly: no dialect may emit the old sentinel. This is the
    /// assertion that fails against pre-fix code for all four.
    /// </summary>
    [Theory]
    [InlineData("postgres")]
    [InlineData("mysql")]
    [InlineData("sqlite")]
    [InlineData("sqlserver")]
    public void SubstringWithoutLength_NeverEmitsTheHardcoded8000(string dialectName)
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(3) == "t";

        string sql = Translate(DialectFor(dialectName), expr);

        Assert.DoesNotContain("8000", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two-argument overload is untouched - the caller gave a length and it is used verbatim.
    /// Guards against "fixing" the one-argument case by rerouting both.
    /// </summary>
    [Theory]
    [InlineData("postgres", "SUBSTRING(product_name FROM 1 FOR 3)")]
    [InlineData("mysql", "SUBSTRING(product_name, 1, 3)")]
    [InlineData("sqlite", "SUBSTR(product_name, 1, 3)")]
    [InlineData("sqlserver", "SUBSTRING(product_name, 1, 3)")]
    public void SubstringWithLength_IsUnchanged(string dialectName, string expected)
    {
        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(0, 3) == "Tes";

        string sql = Translate(DialectFor(dialectName), expr);

        Assert.Contains(expected, sql, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // A dialect that does not implement the optional interface
    // ------------------------------------------------------------------

    /// <summary>
    /// <see cref="ISubstringToEndDialect"/> is optional precisely so an existing external
    /// <see cref="ISqlDialect"/> implementation keeps compiling, so the fallback has to work. It
    /// still may not be 8000: a sentinel is unavoidable there, but it should be one no real value
    /// can exceed.
    /// </summary>
    [Fact]
    public void ADialectWithoutTheOptionalInterface_FallsBackToANonTruncatingLength()
    {
        var legacy = new LegacyOnlyDialect();

        // The premise of the test, asserted rather than assumed: if someone later adds the optional
        // interface to TestDialect this stops exercising the fallback, and should say so loudly
        // rather than quietly pass through the native path instead.
        Assert.IsNotAssignableFrom<ISubstringToEndDialect>(legacy);

        Expression<Func<Product, bool>> expr = p => p.ProductName.Substring(3) == "t";

        string sql = Translate(legacy, expr);

        Assert.Contains("SUBSTRING([product_name], 4, 2147483647)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("8000", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// <see cref="SubstringToEnd.FallbackLength"/> exists so the fallback's sentinel is stated once
    /// rather than repeated at each call site - the failure mode the literal 8000 came from.
    /// </summary>
    [Fact]
    public void TheFallbackLength_IsIntMaxValue()
        => Assert.Equal(int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), SubstringToEnd.FallbackLength);

    /// <summary>
    /// An <see cref="ISqlDialect"/> implementation predating <see cref="ISubstringToEndDialect"/>,
    /// modelling an external implementer. <c>TestDialect</c> is exactly that shape already - it was
    /// written before the optional interface existed and does not implement it - so this derives
    /// from it rather than restating forty forwarding members.
    /// </summary>
    private sealed class LegacyOnlyDialect : TestDialect
    {
    }
}
