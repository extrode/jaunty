using Extrode.Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Tests.Unit.Internals;

/// <summary>
/// The rewrite walker that expands a collection placeholder into <c>(@Ids0, @Ids1)</c> must
/// replace only the placeholder itself: a subtraction is not a comment opener, and a column that
/// shares the parameter's name is not the parameter.
/// </summary>
public class ParameterBinderLiteralAwareRewriteTests
{
    private static string Expand(string sql)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;

        ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 } });

        return command.CommandText;
    }

    [Fact]
    public void ASubtractionBeforeThePlaceholder_DoesNotHideItAsAComment()
    {
        Assert.Equal(
            "SELECT * FROM t WHERE total-1 > 0 AND id IN (@Ids0, @Ids1)",
            Expand("SELECT * FROM t WHERE total-1 > 0 AND id IN @Ids"));
    }

    [Fact]
    public void AColumnNamedLikeTheParameter_IsLeftAlone()
    {
        Assert.Equal(
            "SELECT * FROM t WHERE (Ids > 0) AND id IN (@Ids0, @Ids1)",
            Expand("SELECT * FROM t WHERE (Ids > 0) AND id IN @Ids"));
    }
}
