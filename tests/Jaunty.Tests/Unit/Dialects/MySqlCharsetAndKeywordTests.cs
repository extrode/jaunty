using Jaunty.Dialects;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// Two MySQL dialect defects.
/// <list type="bullet">
/// <item><description>
/// AUD-R35-017 - the case comparisons named utf8mb4 collations, which MySQL rejects with error
/// 1253 on a column of any other character set, so a case-sensitive <c>Contains</c> or an
/// <c>OrdinalIgnoreCase</c> equality failed outright on a latin1 or utf8mb3 column instead of
/// degrading.
/// </description></item>
/// <item><description>
/// AUD-R35-018 - the keyword set predated MySQL 8.0, so a column named <c>rank</c>, <c>rows</c>,
/// <c>system</c> or <c>groups</c> was emitted unquoted and every statement touching it failed to
/// parse on MySQL 8.
/// </description></item>
/// </list>
/// </summary>
public class MySqlCharsetAndKeywordTests
{
    private readonly MySqlDialect _dialect = new();

    // ------------------------------------------------------------------
    // AUD-R35-017
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("utf8mb4")]
    [InlineData("utf8mb3")]
    [InlineData("latin1")]
    [InlineData("COLLATE")]
    public void NoCaseComparisonNamesACharacterSetOrCollation(string forbidden)
    {
        Assert.DoesNotContain(forbidden, _dialect.GenerateCaseSensitiveLike("col", "@p", "\\"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(forbidden, _dialect.GenerateCaseInsensitiveLike("col", "@p", "\\"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(forbidden, _dialect.GenerateCaseInsensitiveEquals("col", "@p"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CaseSensitiveLikeAsksForAByteWiseComparison()
    {
        string sql = _dialect.GenerateCaseSensitiveLike("col", "@p", "\\");

        Assert.Contains("CAST(col AS BINARY)", sql);
        Assert.Contains("LIKE @p", sql);
    }

    [Fact]
    public void CaseSensitiveLikeDoesNotUseTheDeprecatedBinaryOperator()
    {
        // MySQL deprecated the `BINARY expr` operator in 8.0.27 in favour of CAST(... AS BINARY).
        Assert.DoesNotContain("BINARY col", _dialect.GenerateCaseSensitiveLike("col", "@p", "\\"));
    }

    [Fact]
    public void CaseInsensitiveEqualsFoldsBothSides()
    {
        Assert.Equal("LOWER(col) = LOWER(@p)", _dialect.GenerateCaseInsensitiveEquals("col", "@p"));
    }

    [Fact]
    public void CaseInsensitiveEqualsAgreesWithTheOtherDialectsThatFold()
    {
        // PostgreSqlDialect and SQLiteDialect already emitted LOWER()/LOWER(); MySQL was the
        // outlier, and the thing that made it an outlier is what broke on a non-utf8mb4 column.
        Assert.Equal(
            new PostgreSqlDialect().GenerateCaseInsensitiveEquals("col", "@p"),
            _dialect.GenerateCaseInsensitiveEquals("col", "@p"));
    }

    [Fact]
    public void CaseSensitiveLikeStillCarriesTheEscapeClause()
    {
        // The AUD-R35-003 fix must survive the rewrite: a single backslash would escape its own
        // closing quote under MySQL's default sql_mode.
        string sql = _dialect.GenerateCaseSensitiveLike("col", "@p", "\\");

        Assert.EndsWith(@"ESCAPE '\\'", sql);
        Assert.DoesNotContain(@"ESCAPE '\'", sql);
    }

    // ------------------------------------------------------------------
    // AUD-R35-018
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("RANK")]
    [InlineData("DENSE_RANK")]
    [InlineData("ROW_NUMBER")]
    [InlineData("ROW")]
    [InlineData("ROWS")]
    [InlineData("GROUPS")]
    [InlineData("GROUPING")]
    [InlineData("OVER")]
    [InlineData("WINDOW")]
    [InlineData("SYSTEM")]
    [InlineData("RECURSIVE")]
    [InlineData("LEAD")]
    [InlineData("LAG")]
    [InlineData("NTILE")]
    [InlineData("NTH_VALUE")]
    [InlineData("FIRST_VALUE")]
    [InlineData("LAST_VALUE")]
    [InlineData("LATERAL")]
    [InlineData("CUME_DIST")]
    [InlineData("PERCENT_RANK")]
    [InlineData("JSON_TABLE")]
    [InlineData("OF")]
    [InlineData("EMPTY")]
    [InlineData("EXCEPT")]
    public void EveryWordMySql8ReservedIsRecognised(string keyword)
    {
        Assert.True(_dialect.IsKeyword(keyword), $"'{keyword}' is reserved in MySQL 8.0");
        Assert.True(_dialect.IsKeyword(keyword.ToLowerInvariant()), "the lookup is case-insensitive");
    }

    [Theory]
    [InlineData("rank")]
    [InlineData("system")]
    [InlineData("rows")]
    [InlineData("groups")]
    public void AColumnNamedAfterAMySql8KeywordIsQuoted(string columnName)
    {
        string escaped = _dialect.EscapeColumnName(columnName);

        Assert.Equal($"`{columnName}`", escaped);
    }

    [Theory]
    [InlineData("rank")]
    [InlineData("system")]
    public void ATableNamedAfterAMySql8KeywordIsQuoted(string tableName)
    {
        Assert.Equal($"`{tableName}`", _dialect.EscapeTableName(null, tableName));
    }

    [Theory]
    [InlineData("product_name")]
    [InlineData("customer_id")]
    [InlineData("total")]
    public void AnOrdinaryNameIsStillNotQuoted(string columnName)
    {
        // The control: widening the set must not start quoting everything, which would hide a
        // future regression in the keyword lookup itself.
        Assert.False(_dialect.IsKeyword(columnName));
        Assert.Equal(columnName, _dialect.EscapeColumnName(columnName));
    }

    [Fact]
    public void ThePreMySql8WordsAreStillRecognised()
    {
        foreach (string keyword in new[] { "SELECT", "ORDER", "GROUP", "USER", "KEY", "INTERVAL" })
            Assert.True(_dialect.IsKeyword(keyword), $"'{keyword}' was reserved before 8.0 too");
    }
}
