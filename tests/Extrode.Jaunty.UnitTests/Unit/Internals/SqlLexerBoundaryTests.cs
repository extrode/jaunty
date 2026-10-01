using System.Reflection;

using Extrode.Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Tests.Unit.Internals;

public class SqlLexerBoundaryTests
{
    private static readonly string[] Templates =
    [
        "{R}Ids",
        "x {R}Ids",
        "-- {Z}Ids",
        "--",
        "x -",
        "x /",
        "x {R}",
        "/* {Z}Ids",
        "/* {Z}Ids *",
        "/* {Z}Ids */ {R}Ids",
        "/**/ {R}Ids",
        "/* a/b {Z}Ids */ {R}Ids",
        "/* a*b {Z}Ids */ {R}Ids",
        "/* c */* {R}Ids",
        "'a'{R}Ids",
        "\"a\"{R}Ids",
        "[a]{R}Ids",
        "`a`{R}Ids",
        "/* */{R}Ids",
        "/*/ {Z}Ids */ {R}Ids",
        "-- {Z}Ids\n{R}Ids",
        "-- {Z}Ids\r{R}Ids",
        "-- {Z}Ids\r\n{R}Ids",
        "--\n{R}Ids",
        "'{Z}Ids'",
        "'{Z}Ids",
        "'",
        "'a'",
        "'''",
        "''''",
        "'it''s {Z}Ids' {R}Ids",
        "'a' {R}Ids",
        "\"{Z}Ids\" {R}Ids",
        "\"{Z}Ids",
        "\"a\"",
        "[{Z}Ids] {R}Ids",
        "[{Z}Ids",
        "[a]",
        "[a]]{Z}Ids] {R}Ids",
        "`{Z}Ids` {R}Ids",
        "`{Z}Ids",
        "`a`",
        "{Z}{Z}Ids {R}Ids",
        "{Z}{Z}",
        "{Z}{Z}Ids",
        "$$ {Z}Ids $$ {R}Ids",
        "$t$ {Z}Ids $t$ {R}Ids",
        "$$ {Z}Ids",
        "$t$ {Z}Ids $$ {Z}Ids $t$ {R}Ids",
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (string template in Templates)
            yield return new object[] { template };
    }

    private static string Expand(string template, char zone, char real)
        => template.Replace("{Z}", zone.ToString()).Replace("{R}", real.ToString());

    private static (char Zone, char Real)[] SigilPairs(string template)
        => template.StartsWith("{Z}{Z}", StringComparison.Ordinal)
            ? new[] { ('@', '@'), ('@', '$') }
            : new[] { ('@', '@'), ('$', '$'), ('@', '$'), ('$', '@') };

    private static string[] ExpectedNames(string template)
        => template.Contains("{R}Ids") ? new[] { "Ids" } : Array.Empty<string>();

    private static string Replace(string sql, bool backslashEscapes)
    {
        MethodInfo method = typeof(ParameterBinder).GetMethod(
            "ReplaceParametersLiteralAware", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object[] { sql, '@', new Dictionary<string, string> { ["Ids"] = "X" }, backslashEscapes, true })!;
    }

    private static string[] Classic(string sql, bool backslashEscapes)
    {
        MethodInfo method = typeof(SqlParameterParser).GetMethod(
            "ExtractParameterNamesClassic", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (string[])method.Invoke(null, new object[] { sql, backslashEscapes, true })!;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExtractParameterNames_SkipsEveryZone(string template)
    {
        foreach ((char zone, char real) in SigilPairs(template))
        {
            string sql = Expand(template, zone, real);

            Assert.Equal(ExpectedNames(template), SqlParameterParser.ExtractParameterNames(sql));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheClassicWalker_AgreesWithTheSpanWalker(string template)
    {
        foreach ((char zone, char real) in SigilPairs(template))
        {
            string sql = Expand(template, zone, real);

            Assert.Equal(ExpectedNames(template), Classic(sql, false));
            Assert.Equal(SqlParameterParser.ExtractParameterNames(sql, true), Classic(sql, true));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DetectParameterPrefix_IgnoresEveryZone(string template)
    {
        string atReal = Expand(template, '$', '@');
        string dollarReal = Expand(template, '@', '$');

        Assert.Equal("@", ParameterBinder.DetectParameterPrefix(atReal));
        Assert.Equal(template.Contains("{R}Ids") ? "$" : "@", ParameterBinder.DetectParameterPrefix(dollarReal));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CollectionExpansion_RewritesOnlyRealPlaceholders(string template)
    {
        const string Lead = "a IN @Ids ";
        string sql = Lead + Expand(template, '@', '@');
        string expected = "a IN (@Ids0, @Ids1) " + template.Replace("{R}Ids", "(@Ids0, @Ids1)").Replace("{Z}", "@").Replace("{R}", "@");

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Equal(expected, command.CommandText);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ReplaceWalker_RewritesOnlyRealPlaceholders(string template)
    {
        string sql = Expand(template, '@', '@');
        string expected = template.Replace("{R}Ids", "X").Replace("{Z}", "@").Replace("{R}", "@");

        Assert.Equal(expected, Replace(sql, false));
        Assert.Equal(expected, Replace(sql, true));
    }

    [Fact]
    public void ReplaceWalker_AnEmptyNameIsNeverAPlaceholder()
    {
        MethodInfo method = typeof(ParameterBinder).GetMethod(
            "ReplaceParametersLiteralAware", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("a @ b", (string)method.Invoke(null, new object[] { "a @ b", '@', new Dictionary<string, string> { [""] = "X" }, false, true })!);
    }

    [Fact]
    public void DetectParameterPrefix_ASystemVariableDoesNotHideALaterDollarParameter()
        => Assert.Equal("$", ParameterBinder.DetectParameterPrefix("@@xy $Ids"));

    [Theory]
    [InlineData("'a\\' {R}Ids'", false, true)]
    [InlineData("'a\\' {R}Ids'", true, false)]
    [InlineData("\"a\\\" {R}Ids\"", false, true)]
    [InlineData("\"a\\\" {R}Ids\"", true, false)]
    [InlineData("`a\\` {R}Ids", true, true)]
    [InlineData("[a\\] {R}Ids", true, true)]
    [InlineData("'\\", true, false)]
    [InlineData("'a\\'' {R}Ids", true, true)]
    [InlineData("'a\\\\' {R}Ids", true, true)]
    [InlineData("'a\\\\\\' {R}Ids", true, false)]
    [InlineData("'\\x", true, false)]
    public void BackslashEscapes_ApplyOnlyToStringLiteralsOnEnginesThatHaveThem(string template, bool backslashEscapes, bool expectReal)
    {
        string sql = Expand(template, '@', '@');
        string[] expected = expectReal ? new[] { "Ids" } : Array.Empty<string>();

        Assert.Equal(expected, SqlParameterParser.ExtractParameterNames(sql, backslashEscapes));
        Assert.Equal(expected, Classic(sql, backslashEscapes));
        Assert.Equal(expectReal ? sql.Replace("@Ids", "X") : sql, Replace(sql, backslashEscapes));

        string dollar = Expand(template, '@', '$');
        Assert.Equal(expectReal ? "$" : "@", ParameterBinder.DetectParameterPrefix(dollar, backslashEscapes));
    }
}
