using Jaunty.FlatFiles.DuckDB.Dialects;

namespace Jaunty.FlatFiles.DuckDB.Tests.Dialects;

/// <summary>
/// AUD-R34-037 (round-33 carry-forward, coverage). AUD-R32-003 pinned every <c>ISqlDialect</c>
/// implementation's <c>EscapeStringLiteral</c> - in <c>Jaunty.Tests</c>, which does not see this
/// assembly, so DuckDB's was left out. It is the same injection-adjacent helper: the only escaping
/// on the un-parameterized paths (inline literals in projections, GROUP BY keys, HAVING operands).
/// Distinct from the source generator's C#-literal escaper (AUD-R33-011) and from the import
/// dialects' identifier quoting.
/// </summary>
public class DuckDbEscapeStringLiteralTests
{
    private static readonly DuckDbDialect Dialect = new();

    [Fact]
    public void DoublesTheSingleQuote()
    {
        Assert.Equal("O''Brien", Dialect.EscapeStringLiteral("O'Brien"));
    }

    [Fact]
    public void LeavesAPlainValueUntouched()
    {
        Assert.Equal("Beverages", Dialect.EscapeStringLiteral("Beverages"));
    }

    [Fact]
    public void HandlesTheEmptyString()
    {
        Assert.Equal("", Dialect.EscapeStringLiteral(""));
    }

    [Fact]
    public void DoublesEveryQuoteNotJustTheFirst()
    {
        Assert.Equal("''a''b''", Dialect.EscapeStringLiteral("'a'b'"));
    }

    [Fact]
    public void NeutralisesAQuoteFollowedByAStatementTerminator()
    {
        Assert.Equal(
            "x''; DROP TABLE products; --",
            Dialect.EscapeStringLiteral("x'; DROP TABLE products; --"));
    }

    /// <summary>
    /// DuckDB follows the SQL standard here: a backslash in a plain single-quoted literal is an
    /// ordinary character, so doubling the quote is the whole job and the backslash must be left
    /// alone. MySQL is the one dialect where it is not - see the matching case in
    /// <c>EscapeStringLiteralTests</c>.
    /// </summary>
    [Fact]
    public void LeavesABackslashAlone()
    {
        Assert.Equal(@"C:\temp\x''y", Dialect.EscapeStringLiteral(@"C:\temp\x'y"));
    }

    [Fact]
    public void LeavesADoubleQuoteAlone()
    {
        Assert.Equal("say \"hi\"", Dialect.EscapeStringLiteral("say \"hi\""));
    }
}
