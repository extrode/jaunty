using Jaunty.Internals.Parameters;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// <see cref="ParameterBinder.DetectParameterPrefix"/> decides whether a statement's placeholders
/// are <c>@name</c> or <c>$name</c> by walking the SQL, and it has to skip everything that can
/// contain a sigil without being one: comments, string literals, quoted identifiers, <c>@@</c>
/// system variables and Postgres dollar-quoted bodies.
///
/// <para>
/// The 2026-08-27 mutation baseline reported 165 NoCoverage mutants in ParameterBinder.cs, the
/// largest single cluster of them on this walker's lines (612-679) and on
/// <c>TrySkipDollarQuotedString</c> (694-700) — reachable code that no test called, so the
/// mutants were never even run. Every case below puts a decoy sigil inside the construct being
/// skipped and a real <c>$</c> placeholder after it: the decoy is what a broken skip would
/// return.
/// </para>
/// </summary>
public class ParameterPrefixDetectionTests
{
    [Theory]
    [InlineData("", "@")]
    [InlineData("SELECT 1", "@")]
    [InlineData("SELECT * FROM t WHERE id = @id", "@")]
    [InlineData("SELECT * FROM t WHERE id = $1", "$")]
    public void PlainSql_DetectsTheSigilOrFallsBackToAt(string sql, string expected)
    {
        Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql));
    }

    [Theory]
    [InlineData("-- @nope\nSELECT * FROM t WHERE id = $1", "$")]
    [InlineData("-- @nope\rSELECT * FROM t WHERE id = $1", "$")]
    [InlineData("SELECT * FROM t -- @nope", "@")]
    public void LineComments_AreSkipped(string sql, string expected)
    {
        Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql));
    }

    [Theory]
    [InlineData("/* @nope */ SELECT * FROM t WHERE id = $1", "$")]
    [InlineData("SELECT /* @nope */ * FROM t WHERE id = $1", "$")]
    [InlineData("/* @nope", "@")]
    [InlineData("/* @nope *", "@")]
    public void BlockComments_AreSkippedIncludingUnterminatedOnes(string sql, string expected)
    {
        Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql));
    }

    [Theory]
    [InlineData("SELECT '@nope' FROM t WHERE id = $1", "$")]
    [InlineData("SELECT 'it''s @nope' FROM t WHERE id = $1", "$")]
    [InlineData("SELECT \"@nope\" FROM t WHERE id = $1", "$")]
    [InlineData("SELECT [@nope] FROM t WHERE id = $1", "$")]
    [InlineData("SELECT `@nope` FROM t WHERE id = $1", "$")]
    public void LiteralsAndQuotedIdentifiers_AreSkipped(string sql, string expected)
    {
        Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql));
    }

    [Fact]
    public void UnterminatedLiteral_SwallowsTheRestAndFallsBackToAt()
    {
        Assert.Equal("@", ParameterBinder.DetectParameterPrefix("SELECT '@nope FROM t WHERE id = $1"));
    }

    [Theory]
    [InlineData("SELECT @@VERSION, $1 FROM t", "$")]
    [InlineData("SELECT @@ROWCOUNT FROM t", "@")]
    public void DoubleAtSystemVariables_AreNotTreatedAsPlaceholders(string sql, string expected)
    {
        Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql));
    }

    [Theory]
    [InlineData("SELECT $$ @nope $$, $1 FROM t", "$")]
    [InlineData("SELECT $tag$ @nope $tag$, $1 FROM t", "$")]
    [InlineData("SELECT $$ @nope", "@")]
    [InlineData("SELECT $tag$ @nope", "@")]
    public void DollarQuotedBodies_AreSkipped(string sql, string expected)
    {
        Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql));
    }

    [Theory]
    [InlineData("SELECT $ FROM t", "@")]
    [InlineData("SELECT @ FROM t", "@")]
    public void ASigilWithNoNameAfterIt_IsNotAPlaceholder(string sql, string expected)
    {
        Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql));
    }

    [Fact]
    public void ASigilInsideAnIdentifier_IsNotAPlaceholder()
    {
        Assert.Equal("$", ParameterBinder.DetectParameterPrefix("SELECT a@b FROM t WHERE id = $1"));
    }

    [Theory]
    [InlineData(false, "$")]
    [InlineData(true, "@")]
    public void BackslashEscapesChangeWhereTheLiteralEnds(bool backslashEscapes, string expected)
    {
        const string sql = @"SELECT 'ends here\' , $1 FROM t WHERE x = @y";

        Assert.Equal(expected, ParameterBinder.DetectParameterPrefix(sql, backslashEscapes));
    }
}
