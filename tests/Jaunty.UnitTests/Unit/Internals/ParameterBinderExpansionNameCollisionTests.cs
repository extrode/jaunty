using System.Data;
using System.Globalization;

using Jaunty.Internals.Parameters;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Unit.Internals;

/// <summary>
/// AUD-R34-013. IN-clause expansion minted placeholder names as <c>collectionName + index</c>
/// without checking that the minted name was free. Given
/// <c>new { Ids = new[] { 1, 2 }, Ids0 = 5 }</c> against <c>... a IN @Ids AND b = @Ids0</c> the
/// expansion produced <c>IN (@Ids0, @Ids1) AND b = @Ids0</c>, and because <c>BindDynamic</c>
/// consults the expanded names before the property lookup, the genuine <c>@Ids0</c> bound the
/// collection's first element instead of the <c>Ids0</c> property. Wrong value, no exception - the
/// unused-property guard could not see it either, since <c>Ids0</c> does appear in the rewritten
/// SQL. The names are matched case-insensitively, so the collision was too.
/// </summary>
public class ParameterBinderExpansionNameCollisionTests
{
    private static SqliteCommand CreateCommand(string sql, out SqliteConnection connection)
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private static object? ValueOf(SqliteCommand command, string name)
    {
        foreach (SqliteParameter parameter in command.Parameters)
        {
            if (string.Equals(parameter.ParameterName.TrimStart('@', '$', ':'), name, StringComparison.OrdinalIgnoreCase))
                return parameter.Value;
        }

        return null;
    }

    private static List<int> AllValues(SqliteCommand command)
    {
        var values = new List<int>();
        foreach (SqliteParameter parameter in command.Parameters)
            values.Add(Convert.ToInt32(parameter.Value, CultureInfo.InvariantCulture));
        values.Sort();
        return values;
    }

    [Fact]
    public void Bind_ScalarNamedLikeAMintedPlaceholder_KeepsItsOwnValue()
    {
        using SqliteCommand command = CreateCommand("SELECT * FROM t WHERE a IN @Ids AND b = @Ids0", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 }, Ids0 = 5 });

            Assert.Equal(3, command.Parameters.Count);
            Assert.Equal(5, Convert.ToInt32(ValueOf(command, "Ids0"), CultureInfo.InvariantCulture));
            Assert.Equal(new List<int> { 1, 2, 5 }, AllValues(command));
        }
    }

    [Fact]
    public void Bind_ScalarNamedLikeAMintedPlaceholder_IsStillReferencedByTheRewrittenSql()
    {
        using SqliteCommand command = CreateCommand("SELECT * FROM t WHERE a IN @Ids AND b = @Ids0", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 }, Ids0 = 5 });

            Assert.Contains("b = @Ids0", command.CommandText, StringComparison.Ordinal);
            Assert.DoesNotContain("(@Ids0", command.CommandText, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Bind_ScalarNamedLikeAMintedPlaceholder_CollidesCaseInsensitivelyToo()
    {
        using SqliteCommand command = CreateCommand("SELECT * FROM t WHERE a IN @Ids AND b = @IDS1", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 }, IDS1 = 9 });

            Assert.Equal(3, command.Parameters.Count);
            Assert.Equal(9, Convert.ToInt32(ValueOf(command, "IDS1"), CultureInfo.InvariantCulture));
            Assert.Equal(new List<int> { 1, 2, 9 }, AllValues(command));
        }
    }

    [Fact]
    public void Bind_SeveralCollidingScalars_AreAllPreserved()
    {
        using SqliteCommand command = CreateCommand(
            "SELECT * FROM t WHERE a IN @Ids AND b = @Ids0 AND c = @Ids_0", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 }, Ids0 = 5, Ids_0 = 6 });

            Assert.Equal(4, command.Parameters.Count);
            Assert.Equal(5, Convert.ToInt32(ValueOf(command, "Ids0"), CultureInfo.InvariantCulture));
            Assert.Equal(6, Convert.ToInt32(ValueOf(command, "Ids_0"), CultureInfo.InvariantCulture));
            Assert.Equal(new List<int> { 1, 2, 5, 6 }, AllValues(command));
        }
    }

    [Fact]
    public void Bind_NoCollision_MintsTheOrdinaryNames()
    {
        using SqliteCommand command = CreateCommand("SELECT * FROM t WHERE a IN @Ids AND b = @Other", out SqliteConnection connection);
        using (connection)
        {
            ParameterBinder.Bind(command, new { Ids = new[] { 1, 2 }, Other = 5 });

            Assert.Contains("IN (@Ids0, @Ids1)", command.CommandText, StringComparison.Ordinal);
            Assert.Equal(3, command.Parameters.Count);
        }
    }
}
