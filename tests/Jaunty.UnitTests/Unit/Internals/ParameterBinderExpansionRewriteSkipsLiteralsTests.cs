using System.Globalization;

using Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// The literal/comment-aware rewrite that IN-clause expansion runs over the SQL
/// (<c>ReplaceParametersLiteralAware</c>). Its main loop is well covered by
/// <see cref="ParameterBinderExpansionNameCollisionTests"/>, but every <em>skip</em> branch -
/// comments, quoted identifiers, doubled-quote escapes, <c>@@</c> system variables and
/// dollar-quoted strings - was uncovered in the 2026-08-27 mutation baseline, because all of
/// those tests use plain SQL. A naive find/replace would corrupt each of these constructs, which
/// is the whole reason the walker exists.
///
/// <para>
/// The backslash-escape arm is not reachable from here: <c>backslashEscapes</c> is true only for a
/// resolved MySQL dialect, and these commands are SQLite.
/// </para>
/// </summary>
public class ParameterBinderExpansionRewriteSkipsLiteralsTests
{
    private static SqliteCommand CreateCommand(string sql, out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private static string Rewrite(string sql, object parameters)
    {
        using SqliteCommand command = CreateCommand(sql, out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, parameters);
            return command.CommandText;
        }
    }

    private static string RewriteIds(string sql) => Rewrite(sql, new { Ids = new[] { 1, 2 } });

    [Fact]
    public void SingleLineComment_IsCopiedVerbatim()
    {
        string sql = RewriteIds("SELECT * FROM t -- keep @Ids alone\nWHERE a IN @Ids");

        Assert.Contains("-- keep @Ids alone", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleLineComment_EndedByCarriageReturn_IsCopiedVerbatim()
    {
        string sql = RewriteIds("SELECT * FROM t -- keep @Ids alone\r\nWHERE a IN @Ids");

        Assert.Contains("-- keep @Ids alone\r\n", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleLineComment_RunningToEndOfInput_IsCopiedVerbatim()
    {
        string sql = RewriteIds("SELECT * FROM t WHERE a IN @Ids -- trailing @Ids");

        Assert.EndsWith("-- trailing @Ids", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockComment_IsCopiedVerbatim()
    {
        string sql = RewriteIds("SELECT /* keep @Ids alone */ * FROM t WHERE a IN @Ids");

        Assert.Contains("/* keep @Ids alone */", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void UnterminatedBlockComment_SwallowsTheRestOfTheInput()
    {
        string sql = RewriteIds("SELECT * FROM t WHERE a IN @Ids /* @Ids never closed");

        Assert.EndsWith("/* @Ids never closed", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("'literal @Ids'")]
    [InlineData("\"quoted @Ids\"")]
    [InlineData("[bracketed @Ids]")]
    [InlineData("`backticked @Ids`")]
    public void QuotedRun_IsCopiedVerbatim(string quoted)
    {
        string sql = RewriteIds("SELECT " + quoted + " FROM t WHERE a IN @Ids");

        Assert.Contains(quoted, sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void DoubledQuoteInsideALiteral_DoesNotEndTheLiteralEarly()
    {
        string sql = RewriteIds("SELECT 'it''s @Ids all the way' FROM t WHERE a IN @Ids");

        Assert.Contains("'it''s @Ids all the way'", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void UnterminatedLiteral_SwallowsTheRestOfTheInput()
    {
        string sql = RewriteIds("SELECT * FROM t WHERE a IN @Ids AND b = 'never closed @Ids");

        Assert.EndsWith("'never closed @Ids", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemVariable_IsCopiedVerbatim()
    {
        string sql = RewriteIds("SELECT @@VERSION FROM t WHERE a IN @Ids");

        Assert.Contains("@@VERSION", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemVariableNamedLikeTheExpansion_IsNotRewritten()
    {
        string sql = RewriteIds("SELECT @@Ids FROM t WHERE a IN @Ids");

        Assert.Contains("@@Ids", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@@Ids0", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void AnonymousDollarQuotedString_IsCopiedVerbatim()
    {
        string sql = RewriteIds("SELECT $$body @Ids body$$ FROM t WHERE a IN @Ids");

        Assert.Contains("$$body @Ids body$$", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TaggedDollarQuotedString_IsCopiedVerbatim()
    {
        string sql = RewriteIds("SELECT $tag$body @Ids body$tag$ FROM t WHERE a IN @Ids");

        Assert.Contains("$tag$body @Ids body$tag$", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void LoneDollarSign_IsNotTreatedAsAQuoteAndDoesNotStopTheRewrite()
    {
        string sql = RewriteIds("SELECT cost$ FROM t WHERE a IN @Ids");

        Assert.Contains("cost$", sql, StringComparison.Ordinal);
        Assert.Contains("IN (@Ids0, @Ids1)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyCollection_StillSkipsCommentsAndLiterals()
    {
        string sql = Rewrite(
            "SELECT 'keep @Ids' FROM t -- keep @Ids\nWHERE a IN @Ids",
            new { Ids = Array.Empty<int>() });

        Assert.Contains("'keep @Ids'", sql, StringComparison.Ordinal);
        Assert.Contains("-- keep @Ids", sql, StringComparison.Ordinal);
        Assert.Contains("IN (SELECT NULL WHERE 1 = 0)", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheGenuinePlaceholderIsBound()
    {
        using SqliteCommand command = CreateCommand(
            "SELECT '@Ids', @@Ids, $$@Ids$$ FROM t /* @Ids */ WHERE a IN @Ids -- @Ids",
            out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 7, 8 } });

            Assert.Equal(2, command.Parameters.Count);
            var values = new List<int>();
            foreach (SqliteParameter parameter in command.Parameters)
                values.Add(Convert.ToInt32(parameter.Value, CultureInfo.InvariantCulture));
            values.Sort();
            Assert.Equal(new List<int> { 7, 8 }, values);
        }
    }
}
