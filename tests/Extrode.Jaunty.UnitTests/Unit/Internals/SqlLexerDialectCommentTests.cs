using System.Reflection;

using Extrode.Jaunty.Internals.Parameters;

namespace Extrode.Jaunty.Tests.Unit.Internals;

public class SqlLexerDialectCommentTests
{
    public enum Engine
    {
        PostgreSql,
        SqlServer,
        MySql,
        SQLite,
    }

    private static (bool BackslashEscapes, bool DollarSigil, bool BracketIdentifiers, bool NestsBlockComments) Flags(Engine engine) => engine switch
    {
        Engine.PostgreSql => (false, true, false, true),
        Engine.SqlServer => (false, false, true, true),
        Engine.MySql => (true, false, false, false),
        _ => (false, true, true, false),
    };

    private static string[] Span(string sql, Engine engine)
    {
        var (b, d, k, n) = Flags(engine);
        return SqlParameterParser.ExtractParameterNames(sql, b, d, k, n);
    }

    private static string[] Classic(string sql, Engine engine)
    {
        var (b, d, k, n) = Flags(engine);
        MethodInfo method = typeof(SqlParameterParser).GetMethod(
            "ExtractParameterNamesClassic", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string[])method.Invoke(null, new object[] { sql, b, d, k, n })!;
    }

    private static string Detect(string sql, Engine engine)
    {
        var (b, d, k, n) = Flags(engine);
        return ParameterBinder.DetectParameterPrefix(sql, b, d, k, n);
    }

    private static string Replace(string sql, Engine engine)
    {
        var (b, d, k, n) = Flags(engine);
        MethodInfo method = typeof(ParameterBinder).GetMethod(
            "ReplaceParametersLiteralAware", BindingFlags.NonPublic | BindingFlags.Static)!;
        var replacements = new Dictionary<string, string> { ["Id"] = "X", ["Old"] = "Y" };
        return (string)method.Invoke(null, new object[] { sql, '@', replacements, b, d, k, n })!;
    }

    private static void AssertAllWalkers(string sql, Engine engine, string[] expected)
    {
        Assert.Equal(expected, Span(sql, engine));
        Assert.Equal(expected, Classic(sql, engine));

        string expectedRewrite = sql;
        if (expected.Contains("Old"))
            expectedRewrite = expectedRewrite.Replace("@Old", "Y");
        if (expected.Contains("Id"))
            expectedRewrite = expectedRewrite.Replace("@Id", "X");
        Assert.Equal(expectedRewrite, Replace(sql, engine));

        string dollar = sql.Replace("@Id", "$Id");
        Assert.Equal(Flags(engine).DollarSigil && expected.FirstOrDefault() == "Id" ? "$" : "@", Detect(dollar, engine));
    }

    private const string NestedComment = "/* old: /* note */ AND x = @Old */ SELECT * FROM T WHERE id = @Id";

    [Theory]
    [InlineData(Engine.PostgreSql)]
    [InlineData(Engine.SqlServer)]
    public void NestedBlockComment_IsOneComment_OnEnginesThatNest(Engine engine)
        => AssertAllWalkers(NestedComment, engine, new[] { "Id" });

    [Theory]
    [InlineData(Engine.MySql)]
    [InlineData(Engine.SQLite)]
    public void NestedBlockComment_EndsAtTheFirstClose_OnEnginesThatDoNotNest(Engine engine)
        => AssertAllWalkers(NestedComment, engine, new[] { "Old", "Id" });

    [Theory]
    [InlineData(Engine.PostgreSql)]
    [InlineData(Engine.SqlServer)]
    public void NestedBlockComment_DepthReturnsToZeroBeforeLaterParameters(Engine engine)
        => AssertAllWalkers("/* a /* b /* c */ @Old */ @Old */ x = @Id /* @Old */", engine, new[] { "Id" });

    [Theory]
    [InlineData(Engine.PostgreSql)]
    [InlineData(Engine.SqlServer)]
    public void NestedBlockComment_Unterminated_RunsToTheEnd(Engine engine)
        => AssertAllWalkers("x = @Id /* a /* b */ @Old", engine, new[] { "Id" });

    [Theory]
    [InlineData("UPDATE t SET note = E'O\\'Brien' WHERE id = @Id")]
    [InlineData("UPDATE t SET note = e'O\\'Brien' WHERE id = @Id")]
    [InlineData("UPDATE t SET note = E'\\'' WHERE id = @Id")]
    [InlineData("UPDATE t SET note = E'it''s \\' @Old' WHERE id = @Id")]
    public void PostgreSqlEscapeString_TreatsBackslashAsAnEscape(string sql)
        => AssertAllWalkers(sql, Engine.PostgreSql, new[] { "Id" });

    [Fact]
    public void PostgreSqlEscapeString_TheEMustNotEndALongerIdentifier()
        => AssertAllWalkers("SELECT SOME'a\\' WHERE id = @Id", Engine.PostgreSql, new[] { "Id" });

    [Fact]
    public void PostgreSqlEscapeString_APlainLiteralEndingInABackslashIsComplete()
        => AssertAllWalkers("SELECT 'a\\' WHERE id = @Id", Engine.PostgreSql, new[] { "Id" });

    [Theory]
    [InlineData(Engine.SqlServer)]
    public void EscapeStringPrefix_IsIgnoredWithoutTheDollarSigil(Engine engine)
        => AssertAllWalkers("SELECT E'a\\' WHERE id = @Id", engine, new[] { "Id" });

    [Theory]
    [InlineData("SELECT * FROM t # don't filter on @Old\nWHERE id = @Id")]
    [InlineData("SELECT * FROM t # don't filter on @Old\r\nWHERE id = @Id")]
    [InlineData("SELECT * FROM t WHERE id = @Id #@Old")]
    [InlineData("#@Old\nSELECT * FROM t WHERE id = @Id")]
    public void MySqlHashComment_IsALineComment(string sql)
        => AssertAllWalkers(sql, Engine.MySql, new[] { "Id" });

    [Theory]
    [InlineData(Engine.SqlServer, "SELECT * INTO #tmp FROM t WHERE id = @Id")]
    [InlineData(Engine.PostgreSql, "SELECT 5 # 3 WHERE id = @Id")]
    [InlineData(Engine.SQLite, "SELECT 1 # x WHERE id = @Id")]
    public void Hash_IsOrdinaryText_OnOtherEngines(Engine engine, string sql)
        => AssertAllWalkers(sql, engine, new[] { "Id" });
}
