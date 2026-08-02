using System.Data;
using System.Globalization;

using Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

using MySql.Data.MySqlClient;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R34-014. The four sigil walkers treated only a doubled quote as an escape inside a string
/// literal. MySQL and MariaDB accept backslash escapes by default (<c>NO_BACKSLASH_ESCAPES</c>
/// off) - a fact <c>MySqlDialect.EscapeStringLiteral</c> already relies on, since it doubles
/// backslashes - so for <c>... WHERE n = 'it\'s' AND id = @Id</c> the walker ended the literal at
/// the escaped quote and read the following <c>'</c> as a new literal running to the end of the
/// statement, losing <c>@Id</c> and throwing "Unused parameter properties" for legal MySQL SQL.
/// <para>
/// The rule is dialect-specific and cannot simply be turned on everywhere: for every other engine
/// <c>'C:\'</c> is a complete literal ending in a backslash, and treating the backslash as an
/// escape there would swallow the terminator instead. The connections below are never opened - the
/// dialect resolves from the connection type, which is all the binder consults.
/// </para>
/// </summary>
public class ParameterBinderBackslashEscapeTests
{
    private static List<string> ParameterNames(IDbCommand command)
    {
        var names = new List<string>();
        foreach (IDbDataParameter parameter in command.Parameters)
            names.Add(parameter.ParameterName.TrimStart('@', '$', ':'));
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }

    [Fact]
    public void Bind_MySqlBackslashEscapedQuote_StillFindsTheParameterAfterIt()
    {
        using var connection = new MySqlConnection();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = @"SELECT * FROM t WHERE n = 'it\'s' AND id = @Id";

        ParameterBinder.Bind(command, new { Id = 3 });

        Assert.Equal(new List<string> { "Id" }, ParameterNames(command));
        Assert.Equal(3, Convert.ToInt32(command.Parameters[0].Value, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Bind_MySqlLiteralEndingInAnEscapedBackslash_StillFindsTheParameterAfterIt()
    {
        using var connection = new MySqlConnection();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = @"SELECT * FROM t WHERE n = 'a\\' AND id = @Id";

        ParameterBinder.Bind(command, new { Id = 4 });

        Assert.Equal(new List<string> { "Id" }, ParameterNames(command));
    }

    [Fact]
    public void Bind_MySqlBackslashEscapedQuote_StillExpandsACollectionAfterIt()
    {
        using var connection = new MySqlConnection();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = @"SELECT * FROM t WHERE n = 'it\'s' AND id IN @Ids";

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        Assert.Contains("IN (@Ids0, @Ids1)", command.CommandText, StringComparison.Ordinal);
        Assert.Equal(2, command.Parameters.Count);
    }

    [Fact]
    public void Bind_MySqlBackslashEscapedDoubleQuote_StillFindsTheParameterAfterIt()
    {
        using var connection = new MySqlConnection();
        using MySqlCommand command = connection.CreateCommand();
        command.CommandText = @"SELECT * FROM t WHERE n = ""it\"" s"" AND id = @Id";

        ParameterBinder.Bind(command, new { Id = 6 });

        Assert.Equal(new List<string> { "Id" }, ParameterNames(command));
    }

    /// <summary>
    /// The other engines must keep the standard rule: a backslash is an ordinary character, so
    /// <c>'C:\'</c> is a complete literal and the parameter after it is found because the literal
    /// ended where it looks like it ended.
    /// </summary>
    [Fact]
    public void Bind_SqliteTrailingBackslashInALiteral_IsNotAnEscape()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = @"SELECT * FROM t WHERE p = 'C:\' AND id = @Id";

        ParameterBinder.Bind(command, new { Id = 7 });

        Assert.Equal(new List<string> { "Id" }, ParameterNames(command));
    }

    [Fact]
    public void ExtractParameterNames_BackslashEscapesOff_TreatsTheBackslashAsOrdinary()
    {
        string[] names = SqlParameterParser.ExtractParameterNames(@"SELECT * FROM t WHERE p = 'C:\' AND id = @Id");

        Assert.Equal(new[] { "Id" }, names);
    }

    [Fact]
    public void ExtractParameterNames_BackslashEscapesOn_TreatsTheBackslashAsAnEscape()
    {
        string[] names = SqlParameterParser.ExtractParameterNames(@"SELECT * FROM t WHERE n = 'it\'s' AND id = @Id", backslashEscapes: true);

        Assert.Equal(new[] { "Id" }, names);
    }

    [Fact]
    public void ExtractParameterNames_BackslashEscapesOn_DoesNotApplyInsideBacktickedIdentifiers()
    {
        string[] names = SqlParameterParser.ExtractParameterNames(@"SELECT `a\` FROM t WHERE id = @Id", backslashEscapes: true);

        Assert.Equal(new[] { "Id" }, names);
    }
}
