using Jaunty.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R35-163. <c>GetPagingSql</c> appends <c>ORDER BY (SELECT NULL)</c> when the base SQL has no
/// top-level ordering, because SQL Server's OFFSET/FETCH is only valid after an ORDER BY. The check
/// that decides this compared against the literal <c>"ORDER BY"</c> - exactly one space, no word
/// boundary - so any other spacing produced a second ORDER BY after the caller's real one, which is
/// the invalid T-SQL the check exists to prevent.
/// </summary>
public class SqlServerPagingOrderByDetectionTests
{
    private readonly SqlServerDialect _dialect = new();

    private const string Injected = "ORDER BY (SELECT NULL)";

    private string Page(string baseSql) => _dialect.GetPagingSql(baseSql, 10, 20);

    [Theory]
    [InlineData("SELECT * FROM t ORDER BY id")]
    [InlineData("SELECT * FROM t ORDER  BY id")]
    [InlineData("SELECT * FROM t ORDER\nBY id")]
    [InlineData("SELECT * FROM t ORDER\r\nBY id")]
    [InlineData("SELECT * FROM t ORDER\tBY id")]
    [InlineData("SELECT * FROM t ORDER \t\n BY id")]
    [InlineData("SELECT * FROM t order by id")]
    public void AnyWhitespaceBetweenTheKeywords_CountsAsOrdered(string baseSql)
    {
        string sql = Page(baseSql);

        Assert.DoesNotContain(Injected, sql, StringComparison.Ordinal);
        Assert.EndsWith("OFFSET 10 ROWS FETCH NEXT 20 ROWS ONLY", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECT * FROM t")]
    [InlineData("SELECT * FROM t WHERE x = 1")]
    [InlineData("SELECT ROW_NUMBER() OVER (ORDER BY id) AS rn FROM t")]
    [InlineData("SELECT * FROM (SELECT id FROM t ORDER BY id OFFSET 0 ROWS) x")]
    public void WithoutATopLevelOrdering_TheIdiomIsStillInjected(string baseSql)
    {
        Assert.Contains(Injected, Page(baseSql), StringComparison.Ordinal);
    }

    /// <summary>
    /// The trailing boundary: a column or alias whose name merely starts with the keyword is not an
    /// ordering. Without the check, <c>order_by_total</c> would read as one.
    /// </summary>
    [Theory]
    [InlineData("SELECT order_by_total FROM t")]
    [InlineData("SELECT ordering, byline FROM t")]
    [InlineData("SELECT reorder BY_total FROM t")]
    public void AnIdentifierThatMerelyContainsTheKeywords_IsNotAnOrdering(string baseSql)
    {
        Assert.Contains(Injected, Page(baseSql), StringComparison.Ordinal);
    }

    /// <summary>
    /// An unmatched closing paren in a raw fragment used to drive the depth negative, after which
    /// nothing was ever at depth 0 again and a real top-level ORDER BY went unseen.
    /// </summary>
    [Fact]
    public void AnUnmatchedClosingParen_DoesNotHideALaterOrdering()
    {
        string sql = Page("SELECT * FROM dbo.f(1)) AS x ORDER BY id");

        Assert.DoesNotContain(Injected, sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SELECT '-- ORDER BY id' AS c FROM t")]
    [InlineData("SELECT [ORDER BY] FROM t")]
    [InlineData("SELECT 1 FROM t -- ORDER BY id")]
    [InlineData("SELECT 1 FROM t /* ORDER\nBY id */")]
    public void AKeywordInsideAStringLiteralQuotedNameOrComment_IsNotAnOrdering(string baseSql)
    {
        Assert.Contains(Injected, Page(baseSql), StringComparison.Ordinal);
    }
}
