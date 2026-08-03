using Jaunty.Internals.Parameters;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-052. AUD-R34-014 split <c>SqlParameterParserCache</c> in two because the same SQL text
/// parses differently under MySQL/MariaDB, where a backslash escapes the next character inside a
/// string literal. Nothing tested the split: the cache's own tests only ever called the default
/// <c>backslashEscapes: false</c> overload, and the backslash tests either went through
/// <c>ParameterBinder.Bind</c> with SQL no other test reuses or called
/// <c>SqlParameterParser.ExtractParameterNames</c> directly, bypassing the cache entirely. Merging
/// the two caches back into one would therefore have left the whole suite green - cross-contamination
/// only shows up when the <em>same</em> SQL text is parsed under both flag values, which is exactly
/// what every test here does.
/// </summary>
public class SqlParameterParserCacheSplitTests
{
    /// <summary>
    /// A fresh key per test run, so the assertions do not depend on what another test cached first.
    /// The literal's trailing backslash is the whole point: under backslash-escape rules it escapes
    /// the closing quote, so the literal runs on and <c>@Name</c> is inside it; under standard rules
    /// the literal ends and <c>@Name</c> is a parameter.
    /// </summary>
    private static string Sql(string tag) =>
        $"SELECT {tag} FROM t WHERE a = 'x\\' AND b = @Name AND c = @Id";

    [Fact]
    public void TheSameSqlParsesDifferentlyUnderEachFlag()
    {
        string sql = Sql("c1");

        string[] standard = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: false);
        string[] escaped = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: true);

        Assert.NotEqual(standard, escaped);
    }

    /// <summary>
    /// Caching the standard parse first must not serve it to the backslash caller.
    /// </summary>
    [Fact]
    public void AStandardParseIsNotServedToTheBackslashCaller()
    {
        string sql = Sql("c2");

        string[] standard = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: false);
        string[] escaped = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: true);

        Assert.Equal(SqlParameterParser.ExtractParameterNames(sql), standard);
        Assert.Equal(SqlParameterParser.ExtractParameterNames(sql, backslashEscapes: true), escaped);
    }

    /// <summary>
    /// And the other order, which is the one a MySQL-first process reaches.
    /// </summary>
    [Fact]
    public void ABackslashParseIsNotServedToTheStandardCaller()
    {
        string sql = Sql("c3");

        string[] escaped = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: true);
        string[] standard = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: false);

        Assert.Equal(SqlParameterParser.ExtractParameterNames(sql, backslashEscapes: true), escaped);
        Assert.Equal(SqlParameterParser.ExtractParameterNames(sql), standard);
    }

    /// <summary>
    /// The half a merged cache would silently drop: <c>@Name</c> is a parameter under standard
    /// rules and is not one under backslash rules, so a stale hit loses or invents a bind.
    /// </summary>
    [Fact]
    public void TheParameterSetsDifferByTheOneInsideTheRunOnLiteral()
    {
        string sql = Sql("c4");

        string[] standard = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: false);
        string[] escaped = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: true);

        Assert.Contains("Name", standard);
        Assert.DoesNotContain("Name", escaped);
    }

    /// <summary>
    /// Repeated calls under one flag are stable, which is what makes the cache a cache - the
    /// control for the four tests above.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedCallsUnderOneFlagAgree(bool backslashEscapes)
    {
        string sql = Sql("c5");

        string[] first = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes);
        string[] second = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes);

        Assert.Same(first, second);
    }

    /// <summary>
    /// SQL with no backslash at all parses identically either way, so the split costs nothing for
    /// the overwhelmingly common case.
    /// </summary>
    [Fact]
    public void SqlWithoutABackslashParsesTheSameWayUnderBothFlags()
    {
        const string sql = "SELECT c6 FROM t WHERE a = 'x' AND b = @Name AND c = @Id";

        Assert.Equal(
            SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: false),
            SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: true));
    }
}
