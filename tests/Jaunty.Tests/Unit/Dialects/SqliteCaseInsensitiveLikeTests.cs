using Jaunty.Dialects;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R25 (B4-7): <c>SQLiteDialect.GenerateCaseInsensitiveLike</c> emitted
/// <c>col LIKE @p ESCAPE '\'</c> while the dialect's own pattern formatters emit <em>GLOB</em>
/// patterns (<c>*value*</c>, escaping via <c>[[]</c>/<c>[*]</c>/<c>[?]</c>) to pair with
/// <c>GenerateCaseSensitiveLike</c>'s GLOB. Pairing them - the only pairing <c>ISqlDialect</c>
/// offers - produced <c>col LIKE '*abc*'</c>, where <c>*</c> is a literal: it matched nothing, and
/// the caller's own <c>%</c> and <c>_</c> were left unescaped.
///
/// <para>
/// The method had no production caller when the audit found it, so nothing was broken yet. Wiring
/// <c>StringComparison</c> through <c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c> in the same
/// round gave it one, which is what makes this load-bearing: without the fix, an OrdinalIgnoreCase
/// search on SQLite would have gone from returning case-sensitive results to returning none.
/// </para>
///
/// <para>
/// These tests execute real SQL through Microsoft.Data.Sqlite rather than asserting on strings,
/// because the defect was that a perfectly well-formed statement matched no rows.
/// </para>
/// </summary>
public class SqliteCaseInsensitiveLikeTests
{
    private static readonly SQLiteDialect Dialect = new();

    // ------------------------------------------------------------------
    // Executed
    // ------------------------------------------------------------------

    [Fact]
    public void CaseInsensitiveContains_MatchesBothCasings()
    {
        Assert.Equal(2, CountMatching(
            Dialect.GenerateCaseInsensitiveLike("name", "@p", "\\"),
            Dialect.FormatContainsPattern("idget")));
    }

    [Fact]
    public void CaseSensitiveContains_MatchesOnlyTheExactCasing()
    {
        Assert.Equal(1, CountMatching(
            Dialect.GenerateCaseSensitiveLike("name", "@p", "\\"),
            Dialect.FormatContainsPattern("idget")));
    }

    [Fact]
    public void CaseInsensitiveStartsWith_MatchesBothCasings()
    {
        Assert.Equal(2, CountMatching(
            Dialect.GenerateCaseInsensitiveLike("name", "@p", "\\"),
            Dialect.FormatStartsWithPattern("widget")));
    }

    [Fact]
    public void CaseInsensitiveEndsWith_MatchesRegardlessOfCase()
    {
        Assert.Equal(1, CountMatching(
            Dialect.GenerateCaseInsensitiveLike("name", "@p", "\\"),
            Dialect.FormatEndsWithPattern("BETA")));
    }

    [Fact]
    public void CaseInsensitiveContains_MatchingNothing_ReturnsZero()
    {
        // Guards against the fix over-matching: a genuinely absent substring must still find nothing.
        Assert.Equal(0, CountMatching(
            Dialect.GenerateCaseInsensitiveLike("name", "@p", "\\"),
            Dialect.FormatContainsPattern("sprocket")));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("?")]
    [InlineData("[")]
    public void CaseInsensitiveContains_TreatsGlobMetacharactersLiterally(string metacharacter)
    {
        // EscapeGlobPattern's escapes have to survive the LOWER() folding. If they did not, "*"
        // would become a wildcard and match all three rows instead of the one that contains it.
        Assert.Equal(1, CountMatching(
            Dialect.GenerateCaseInsensitiveLike("name", "@p", "\\"),
            Dialect.FormatContainsPattern(metacharacter)));
    }

    [Fact]
    public void CaseInsensitiveContains_DoesNotTreatPercentAsAWildcard()
    {
        // The old LIKE form left % unescaped, so a caller's literal % silently became "match anything".
        Assert.Equal(1, CountMatching(
            Dialect.GenerateCaseInsensitiveLike("name", "@p", "\\"),
            Dialect.FormatContainsPattern("%")));
    }

    // ------------------------------------------------------------------
    // Shape
    // ------------------------------------------------------------------

    [Fact]
    public void CaseInsensitiveLike_UsesGlobSoItPairsWithTheGlobPatternFormatters()
    {
        var sql = Dialect.GenerateCaseInsensitiveLike("name", "@p", "\\");

        Assert.Contains("GLOB", sql);
        Assert.DoesNotContain("LIKE", sql);
    }

    /// <summary>
    /// Rows: one lowercase-"idget" match, one uppercase-"IDGET" match, and one row holding every
    /// GLOB/LIKE metacharacter so the escaping assertions have exactly one row to find.
    /// </summary>
    private static int CountMatching(string predicateSql, string pattern)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (SqliteCommand seed = connection.CreateCommand())
        {
            seed.CommandText = """
                CREATE TABLE t (name TEXT);
                INSERT INTO t (name) VALUES ('Widget Alpha'), ('WIDGET BETA'), ('meta *?[% chars');
                """;
            seed.ExecuteNonQuery();
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM t WHERE {predicateSql}";
        command.Parameters.AddWithValue("@p", pattern);
        return Convert.ToInt32(command.ExecuteScalar());
    }
}
