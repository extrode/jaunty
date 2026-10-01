using System.Data;
using System.Reflection;

using Extrode.Jaunty.Internals.Parameters;

using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

using Npgsql;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// Round 38 follow-up: every SQL walker read <c>[</c> as a quoted identifier whatever the engine,
/// so PostgreSQL's <c>ARRAY[@a]</c> and <c>tags[@i]</c> and DuckDB's <c>[@a]</c> list literal hid
/// their placeholders from binding.
/// </summary>
public class ParameterBinderBracketTests
{
    private static List<string> ParameterNames(IDbCommand command)
    {
        var names = new List<string>();
        foreach (IDbDataParameter parameter in command.Parameters)
            names.Add(parameter.ParameterName.TrimStart('@', '$', ':'));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private static string[] Classic(string sql, bool bracketIdentifiers)
    {
        MethodInfo method = typeof(SqlParameterParser).GetMethod(
            "ExtractParameterNamesClassic", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string[])method.Invoke(null, new object[] { sql, false, true, bracketIdentifiers })!;
    }

    [Theory]
    [InlineData("SELECT ARRAY[@a, @b]", new[] { "a", "b" })]
    [InlineData("SELECT tags[@i] FROM t WHERE id = @Id", new[] { "i", "Id" })]
    public void ExtractParameterNames_BracketsOff_FindsPlaceholdersInsideBrackets(string sql, string[] expected)
    {
        Assert.Equal(expected, SqlParameterParser.ExtractParameterNames(sql, bracketIdentifiers: false));
        Assert.Equal(expected, Classic(sql, bracketIdentifiers: false));
    }

    [Fact]
    public void ExtractParameterNames_BracketsOn_SkipsABracketedIdentifier()
    {
        const string sql = "SELECT [@odd] FROM t WHERE id = @Id";

        Assert.Equal(new[] { "Id" }, SqlParameterParser.ExtractParameterNames(sql));
        Assert.Equal(new[] { "Id" }, Classic(sql, bracketIdentifiers: true));
    }

    [Fact]
    public void DetectParameterPrefix_BracketsOff_SeesTheSigilInsideBrackets()
    {
        const string sql = "SELECT arr[$i] FROM t WHERE id IN @Ids";

        Assert.Equal("$", ParameterBinder.DetectParameterPrefix(sql, bracketIdentifiers: false));
        Assert.Equal("@", ParameterBinder.DetectParameterPrefix(sql));
    }

    [Fact]
    public void Cache_SameSql_ParsesDifferentlyPerBracketFlag()
    {
        const string sql = "SELECT tags[@bracketsplit] FROM t WHERE id = @Id";

        Assert.Equal(new[] { "Id" }, SqlParameterParserCache.GetOrAdd(sql));
        Assert.Equal(new[] { "bracketsplit", "Id" }, SqlParameterParserCache.GetOrAdd(sql, bracketIdentifiers: false));
        Assert.Equal(new[] { "Id" }, SqlParameterParserCache.GetOrAdd(sql));
    }

    [Fact]
    public void Cache_BackslashEscapes_HonoursTheBracketFlag()
    {
        const string sql = "SELECT tags[@backslashbracket] FROM t WHERE id = @Id";

        Assert.Equal(new[] { "Id" }, SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: true));
        Assert.Equal(new[] { "backslashbracket", "Id" }, SqlParameterParserCache.GetOrAdd(sql, backslashEscapes: true, bracketIdentifiers: false));
    }

    [Fact]
    public void Bind_PostgreSqlArraySubscript_BindsThePlaceholderInside()
    {
        using var connection = new NpgsqlConnection();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT tags[@I] FROM t WHERE id = @Id";

        ParameterBinder.Bind(command, new { I = 1, Id = 2 });

        Assert.Equal(new List<string> { "I", "Id" }, ParameterNames(command));
    }

    [Fact]
    public void Bind_PostgreSqlArrayConstructor_ExpandsTheCollectionInside()
    {
        using var connection = new NpgsqlConnection();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM t WHERE id = ANY(ARRAY[@Ids])";

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Equal("SELECT * FROM t WHERE id = ANY(ARRAY[(@Ids0, @Ids1)])", command.CommandText);
        Assert.Equal(new List<string> { "Ids0", "Ids1" }, ParameterNames(command));
    }

    [Fact]
    public void Bind_SqlServerBracketIdentifier_IsLeftAlone()
    {
        using var connection = new SqlConnection();
        using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT [@Ids] FROM t WHERE id IN @Ids";

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Equal("SELECT [@Ids] FROM t WHERE id IN (@Ids0, @Ids1)", command.CommandText);
    }

    [Fact]
    public void Bind_SqliteBracketIdentifier_IsLeftAlone()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT [@Ids] FROM t WHERE id IN @Ids";

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Equal("SELECT [@Ids] FROM t WHERE id IN (@Ids0, @Ids1)", command.CommandText);
    }
}
