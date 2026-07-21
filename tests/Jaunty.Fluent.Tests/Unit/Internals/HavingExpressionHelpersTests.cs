using Jaunty.Dialects;
using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Tests.Unit.Internals;

/// <summary>
/// Unit tests for HavingExpressionHelpers.FormatLiteral.
/// </summary>
public class HavingExpressionHelpersTests
{
    [Fact]
    public void FormatLiteral_MySqlDialect_StringEndingInBackslash_EscapesBackslashBeforeQuote()
    {
        // Regression test: MySQL/MariaDB (without NO_BACKSLASH_ESCAPES) treats backslash as an
        // in-string escape character. Escaping only the single quote (the pre-fix behavior)
        // would let a value ending in an odd number of backslashes escape the closing quote and
        // break out of the string literal. The backslash must be doubled first.
        var dialect = new MySqlDialect();

        string result = HavingExpressionHelpers.FormatLiteral("evil\\", dialect);

        Assert.Equal("'evil\\\\'", result);
    }

    [Fact]
    public void FormatLiteral_MySqlDialect_StringWithQuote_EscapesQuote()
    {
        var dialect = new MySqlDialect();

        string result = HavingExpressionHelpers.FormatLiteral("O'Brien", dialect);

        Assert.Equal("'O''Brien'", result);
    }

    [Fact]
    public void FormatLiteral_SQLiteDialect_StringEndingInBackslash_DoesNotDoubleBackslash()
    {
        // SQLite has no backslash-escape convention, so only the single quote is doubled.
        var dialect = new SQLiteDialect();

        string result = HavingExpressionHelpers.FormatLiteral("evil\\", dialect);

        Assert.Equal("'evil\\'", result);
    }

    [Fact]
    public void FormatLiteral_NullValue_ReturnsSqlNull()
    {
        var dialect = new SQLiteDialect();

        string result = HavingExpressionHelpers.FormatLiteral(null, dialect);

        Assert.Equal("NULL", result);
    }
}
