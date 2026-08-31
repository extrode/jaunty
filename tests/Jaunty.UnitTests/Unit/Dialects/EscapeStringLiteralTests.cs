using Jaunty.Dialects;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R32-003. <c>EscapeStringLiteral</c> is the only escaping helper on <see cref="ISqlDialect"/>
/// used on an un-parameterized path (inline literals in projections, GROUP BY keys and HAVING
/// operands), and only the MySQL backslash case had a test - and that one lives in the Fluent
/// suite, reached through <c>HavingExpressionHelpers</c> rather than the dialect itself. Getting
/// the escape wrong here is injection-adjacent, so each implementation is pinned directly.
/// </summary>
public class EscapeStringLiteralTests
{
    public static TheoryData<ISqlDialect> AllDialects() =>
    [
        new SqlServerDialect(),
        new SQLiteDialect(),
        new PostgreSqlDialect(),
        new MySqlDialect(),
    ];

    [Theory]
    [MemberData(nameof(AllDialects))]
    public void EveryDialect_DoublesTheSingleQuote(ISqlDialect dialect)
    {
        Assert.Equal("O''Brien", dialect.EscapeStringLiteral("O'Brien"));
    }

    [Theory]
    [MemberData(nameof(AllDialects))]
    public void EveryDialect_LeavesAPlainValueUntouched(ISqlDialect dialect)
    {
        Assert.Equal("Beverages", dialect.EscapeStringLiteral("Beverages"));
    }

    [Theory]
    [MemberData(nameof(AllDialects))]
    public void EveryDialect_HandlesTheEmptyString(ISqlDialect dialect)
    {
        Assert.Equal("", dialect.EscapeStringLiteral(""));
    }

    [Theory]
    [MemberData(nameof(AllDialects))]
    public void EveryDialect_DoublesEveryQuoteNotJustTheFirst(ISqlDialect dialect)
    {
        Assert.Equal("''a''b''", dialect.EscapeStringLiteral("'a'b'"));
    }

    [Theory]
    [MemberData(nameof(AllDialects))]
    public void EveryDialect_NeutralisesAQuoteFollowedByAStatementTerminator(ISqlDialect dialect)
    {
        // The classic break-out payload. After escaping, the closing quote of the literal is no
        // longer reachable from inside the value.
        Assert.Equal("x''; DROP TABLE products; --", dialect.EscapeStringLiteral("x'; DROP TABLE products; --"));
    }

    [Fact]
    public void MySql_DoublesTheBackslashBeforeTheQuote()
    {
        // MySQL/MariaDB without NO_BACKSLASH_ESCAPES treats backslash as an in-string escape, so
        // a value ending in an odd number of backslashes would otherwise escape the closing quote.
        // Order matters: backslashes first, then quotes.
        Assert.Equal("evil\\\\", new MySqlDialect().EscapeStringLiteral("evil\\"));
    }

    [Fact]
    public void MySql_BackslashQuotePayload_EscapesBoth()
    {
        Assert.Equal("\\\\''", new MySqlDialect().EscapeStringLiteral("\\'"));
    }

    [Theory]
    [InlineData(typeof(SqlServerDialect))]
    [InlineData(typeof(SQLiteDialect))]
    [InlineData(typeof(PostgreSqlDialect))]
    public void NonMySqlDialects_LeaveTheBackslashAlone(Type dialectType)
    {
        // Standard SQL has no backslash escape; doubling it here would corrupt the value.
        var dialect = (ISqlDialect)Activator.CreateInstance(dialectType)!;

        Assert.Equal("C:\\temp", dialect.EscapeStringLiteral("C:\\temp"));
    }
}
