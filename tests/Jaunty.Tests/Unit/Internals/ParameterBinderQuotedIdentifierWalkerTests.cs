using System.Data;

using Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R34-012. AUD-R31-001 taught <c>SqlParameterParser</c>'s two walkers to skip backtick-quoted
/// identifiers, but the two walkers duplicated inside <c>ParameterBinder</c> -
/// <c>DetectParameterPrefix</c> and <c>ReplaceParametersLiteralAware</c>, both of which claim in
/// their comments to use "the same tokenization rules as SqlParameterParser" - were left behind, so
/// only two of the four sigil walkers were fixed. A backtick fell through to the default copy arm,
/// which means an apostrophe inside a backticked identifier opened a phantom string literal that
/// swallowed the rest of the statement and left the IN-clause placeholder unexpanded.
/// </summary>
public class ParameterBinderQuotedIdentifierWalkerTests
{
    private static SqliteCommand CreateCommand(string sql, out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    [Fact]
    public void DetectParameterPrefix_ApostropheInsideABacktickedIdentifier_StillFindsTheSigil()
    {
        Assert.Equal("$", ParameterBinder.DetectParameterPrefix("SELECT `it's` FROM t WHERE id IN $Ids"));
    }

    [Fact]
    public void DetectParameterPrefix_SigilInsideABacktickedIdentifier_IsNotTheParameterPrefix()
    {
        Assert.Equal("$", ParameterBinder.DetectParameterPrefix("SELECT `@col` FROM t WHERE id IN $Ids"));
    }

    [Fact]
    public void DetectParameterPrefix_PlainAtSigilSql_IsUnchanged()
    {
        Assert.Equal("@", ParameterBinder.DetectParameterPrefix("SELECT * FROM t WHERE id = @Id"));
    }

    [Fact]
    public void Bind_ApostropheInsideABacktickedIdentifier_StillExpandsTheCollection()
    {
        using SqliteCommand command = CreateCommand("SELECT `it's` FROM t WHERE id IN @Ids", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

            Assert.Contains("IN (@Ids0, @Ids1)", command.CommandText, StringComparison.Ordinal);
            Assert.Equal(2, command.Parameters.Count);
            Assert.Equal(1, Convert.ToInt32(command.Parameters[0].Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(2, Convert.ToInt32(command.Parameters[1].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [Fact]
    public void Bind_SigilInsideABacktickedIdentifier_LeavesTheIdentifierAlone()
    {
        using SqliteCommand command = CreateCommand("SELECT `@Ids` FROM t WHERE id IN @Ids", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

            Assert.Contains("SELECT `@Ids` FROM", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("IN (@Ids0, @Ids1)", command.CommandText, StringComparison.Ordinal);
            Assert.Equal(2, command.Parameters.Count);
        }
    }

    [Fact]
    public void Bind_DoubledBacktickInsideABacktickedIdentifier_IsNotAnEarlyTerminator()
    {
        using SqliteCommand command = CreateCommand("SELECT `od``d's` FROM t WHERE id IN @Ids", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 7 } });

            Assert.Contains("SELECT `od``d's` FROM", command.CommandText, StringComparison.Ordinal);
            Assert.Contains("IN (@Ids0)", command.CommandText, StringComparison.Ordinal);
            Assert.Single(command.Parameters);
        }
    }

    [Fact]
    public void Bind_NoBackticks_StillExpands()
    {
        using SqliteCommand command = CreateCommand("SELECT * FROM t WHERE id IN @Ids", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 1, 2, 3 } });

            Assert.Contains("IN (@Ids0, @Ids1, @Ids2)", command.CommandText, StringComparison.Ordinal);
            Assert.Equal(3, command.Parameters.Count);
        }
    }
}
