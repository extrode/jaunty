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

    // AUD-R32-001: bool was hardcoded to "1"/"0", which is not a boolean on PostgreSQL.

    [Theory]
    [InlineData(true, "TRUE")]
    [InlineData(false, "FALSE")]
    public void FormatLiteral_PostgreSqlDialect_Bool_UsesTrueFalse(bool value, string expected)
    {
        var dialect = new PostgreSqlDialect();

        string result = HavingExpressionHelpers.FormatLiteral(value, dialect);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "1")]
    [InlineData(false, "0")]
    public void FormatLiteral_SqlServerDialect_Bool_StillUsesOneZero(bool value, string expected)
    {
        var dialect = new SqlServerDialect();

        string result = HavingExpressionHelpers.FormatLiteral(value, dialect);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "1")]
    [InlineData(false, "0")]
    public void FormatLiteral_SqliteDialect_Bool_StillUsesOneZero(bool value, string expected)
    {
        var dialect = new SQLiteDialect();

        string result = HavingExpressionHelpers.FormatLiteral(value, dialect);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "1")]
    [InlineData(false, "0")]
    public void FormatLiteral_MySqlDialect_Bool_StillUsesOneZero(bool value, string expected)
    {
        var dialect = new MySqlDialect();

        string result = HavingExpressionHelpers.FormatLiteral(value, dialect);

        Assert.Equal(expected, result);
    }

    // AUD-R34-006: only DateTime was quoted. Every other date/time type implements IFormattable
    // and so fell into the generic arm and came out bare. A bare DateOnly is the dangerous one -
    // 2026-08-02 parses as arithmetic and silently computes 2016 rather than failing.

    [Fact]
    public void FormatLiteral_DateTime_IsQuoted()
    {
        string result = HavingExpressionHelpers.FormatLiteral(new DateTime(2026, 8, 2, 13, 45, 7), new SQLiteDialect());

        Assert.Equal("'2026-08-02 13:45:07'", result);
    }

    [Fact]
    public void FormatLiteral_DateTimeOffset_IsQuoted()
    {
        var value = new DateTimeOffset(2026, 8, 2, 13, 45, 7, TimeSpan.FromHours(1));

        string result = HavingExpressionHelpers.FormatLiteral(value, new SQLiteDialect());

        Assert.Equal("'2026-08-02 13:45:07+01:00'", result);
    }

    [Fact]
    public void FormatLiteral_TimeSpan_IsQuoted()
    {
        string result = HavingExpressionHelpers.FormatLiteral(new TimeSpan(13, 45, 7), new SQLiteDialect());

        Assert.Equal("'13:45:07'", result);
    }

    [Fact]
    public void FormatLiteral_Guid_IsQuoted()
    {
        var value = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

        string result = HavingExpressionHelpers.FormatLiteral(value, new SQLiteDialect());

        Assert.Equal("'0f8fad5b-d9cb-469f-a165-70867728950e'", result);
    }

    [Fact]
    public void FormatLiteral_Char_IsQuotedAndEscaped()
    {
        string result = HavingExpressionHelpers.FormatLiteral('\'', new SQLiteDialect());

        Assert.Equal("''''", result);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void FormatLiteral_DateOnly_IsQuoted()
    {
        string result = HavingExpressionHelpers.FormatLiteral(new DateOnly(2026, 8, 2), new SQLiteDialect());

        Assert.Equal("'2026-08-02'", result);
    }

    [Fact]
    public void FormatLiteral_TimeOnly_IsQuoted()
    {
        string result = HavingExpressionHelpers.FormatLiteral(new TimeOnly(13, 45, 7), new SQLiteDialect());

        Assert.Equal("'13:45:07'", result);
    }
#endif
}
