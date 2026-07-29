using Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 3, medium). <c>@</c> and <c>$</c> were treated as parameter sigils wherever they
/// appeared, with no check that the position was a placeholder position at all. Both are legal
/// <em>inside</em> identifiers - <c>$</c> in SQL Server, PostgreSQL, Oracle and SQLite, <c>@</c> in
/// SQL Server - so ordinary SQL against a legacy or generated schema simply did not work.
///
/// <para>
/// Scanning <c>sales$2024</c>, the parser walked <c>s,a,l,e,s</c> as ordinary characters, hit
/// <c>$</c>, failed the dollar-quote probe (no second <c>$</c> arrives), and emitted a phantom
/// parameter named <c>2024</c>. <c>BuildTemplate</c> then threw, because no property matched it -
/// and the message said <c>'@2024'</c> while the character actually scanned was <c>$</c>, so it
/// misdirected anyone trying to find the offending text.
/// </para>
///
/// <para>
/// The rule was duplicated across four sites - the span parser, the classic parser,
/// <c>DetectParameterPrefix</c> and <c>ReplaceParametersLiteralAware</c> - each with the same gap.
/// The finding's own requirement was that a fix cover all four "or they will disagree", so the
/// predicate is shared rather than copied, and these tests exercise each site.
/// </para>
/// </summary>
public class SigilInsideIdentifierTests
{
    // ------------------------------------------------------------------
    // Extraction - the span and classic parsers
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("SELECT * FROM sales$2024 WHERE id = @Id", new[] { "Id" })]
    [InlineData("SELECT * FROM sales$2024", new string[0])]
    [InlineData("SELECT a$b, c$d FROM t", new string[0])]
    [InlineData("SELECT * FROM my$table JOIN other$table ON my$table.id = other$table.id WHERE x = @X", new[] { "X" })]
    // '@' is legal inside a SQL Server identifier too, and had the identical gap.
    [InlineData("SELECT * FROM tab@le WHERE id = @Id", new[] { "Id" })]
    [InlineData("SELECT col@1 FROM t", new string[0])]
    public void ASigilInsideAnIdentifier_IsNotAParameter(string sql, string[] expected)
        => Assert.Equal(expected, SqlParameterParser.ExtractParameterNames(sql));

    /// <summary>
    /// The placeholder cases must keep working - a sigil is a placeholder whenever what precedes it
    /// is not an identifier character, which covers every real position: statement start,
    /// whitespace, operators, commas, opening parens.
    /// </summary>
    [Theory]
    [InlineData("SELECT * FROM t WHERE id = @Id", new[] { "Id" })]
    [InlineData("@Id", new[] { "Id" })]
    [InlineData("SELECT * FROM t WHERE a=@A AND b=@B", new[] { "A", "B" })]
    [InlineData("INSERT INTO t VALUES (@A,@B)", new[] { "A", "B" })]
    [InlineData("SELECT * FROM t WHERE a = @A+@B", new[] { "A", "B" })]
    [InlineData("SELECT * FROM t WHERE a = $1 AND b = $2", new[] { "1", "2" })]
    [InlineData("SELECT * FROM t WHERE (a=@A)", new[] { "A" })]
    public void APlaceholderSigil_IsStillAParameter(string sql, string[] expected)
        => Assert.Equal(expected, SqlParameterParser.ExtractParameterNames(sql));

    /// <summary>
    /// Dollar-quoted strings still take priority over both rules - that is AUD-R6's fix and it must
    /// survive this one.
    /// </summary>
    [Theory]
    [InlineData("SELECT $$SELECT 1$$ FROM t WHERE id = @Id", new[] { "Id" })]
    [InlineData("SELECT $tag$ @NotAParam $tag$ FROM t WHERE id = @Id", new[] { "Id" })]
    public void DollarQuotedStrings_AreStillSkipped(string sql, string[] expected)
        => Assert.Equal(expected, SqlParameterParser.ExtractParameterNames(sql));

    /// <summary>SQL Server's <c>@@IDENTITY</c> family is still not a parameter.</summary>
    [Fact]
    public void GlobalVariables_AreStillNotParameters()
        => Assert.Empty(SqlParameterParser.ExtractParameterNames("SELECT @@IDENTITY"));

    // ------------------------------------------------------------------
    // DetectParameterPrefix
    // ------------------------------------------------------------------

    /// <summary>
    /// The third site. A <c>$</c> inside an identifier used to make the whole statement look like a
    /// <c>$</c>-prefixed one, so IN-clause expansion then rewrote the wrong sigil.
    /// </summary>
    [Theory]
    [InlineData("SELECT * FROM sales$2024 WHERE id = @Id", "@")]
    [InlineData("SELECT * FROM sales$2024 WHERE id IN @Ids", "@")]
    [InlineData("SELECT * FROM t WHERE id = $1", "$")]
    [InlineData("SELECT * FROM t WHERE id = @Id", "@")]
    public void DetectParameterPrefix_IgnoresSigilsInsideIdentifiers(string sql, string expected)
        => Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql));

    // ------------------------------------------------------------------
    // End to end, against a real connection
    // ------------------------------------------------------------------

    /// <summary>
    /// The measured reproduction, kept as a test. Before the fix this threw
    /// <c>ArgumentException: No property found on type … matching SQL parameter '@2024'</c>.
    /// </summary>
    [Fact]
    public void ATableNameContainingADollar_CanBeQueried()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (SqliteCommand seed = connection.CreateCommand())
        {
            seed.CommandText = """
                CREATE TABLE "sales$2024" (id INTEGER, name TEXT);
                INSERT INTO "sales$2024" VALUES (5, 'five'), (6, 'six');
                """;
            seed.ExecuteNonQuery();
        }

        List<IDictionary<string, object?>> rows =
            [.. connection.QueryPartialList("SELECT * FROM \"sales$2024\" WHERE id = @Id", new { Id = 5 })];

        Assert.Equal("five", Assert.Single(rows)["name"]);
    }

    /// <summary>
    /// The control the finding used: the same statement against a table that does not exist fails
    /// with SQLite's own error, not Jaunty's - which is what proves the case above was Jaunty
    /// mis-parsing rather than the provider rejecting the name.
    /// </summary>
    [Fact]
    public void TheControl_FailsWithTheProvidersError_NotJauntys()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        SqliteException ex = Assert.Throws<SqliteException>(
            () => connection.QueryPartialList("SELECT * FROM \"sales$2024\" WHERE id = @Id", new { Id = 5 }).ToList());

        Assert.Contains("no such table", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
