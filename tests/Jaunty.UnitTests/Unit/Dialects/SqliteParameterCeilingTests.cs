using Jaunty.Dialects;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Dialects;

/// <summary>
/// AUD-R26 (batch 4, medium/bug). <c>SQLiteDialect.MaxParametersPerStatement</c> reported 999 -
/// <c>SQLITE_MAX_VARIABLE_NUMBER</c>'s default before SQLite 3.32 (2020) - understating the real
/// limit by a factor of 33. Because the value is enforced as a hard rejection and not only as a
/// batch-sizing hint, Jaunty refused <c>IN</c>-clause expansions the provider ran without
/// complaint: a 1,000-id <c>WHERE Id IN @Ids</c> failed with advice to batch, on a statement the
/// engine would have executed.
///
/// <para>
/// The first test is the measurement, kept executable rather than written down. It binds the
/// dialect's own reported ceiling to a real <c>IN</c> list and executes it, then does the same one
/// parameter over. If a future package bump moves the native limit, this fails and says so instead
/// of the ceiling silently drifting away from the engine again.
/// </para>
/// </summary>
public class SqliteParameterCeilingTests
{
    private static SqliteConnection OpenMemory()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static void ExecuteInList(SqliteConnection connection, int parameterCount)
    {
        using SqliteCommand command = connection.CreateCommand();
        var names = new string[parameterCount];
        for (int i = 0; i < parameterCount; i++)
        {
            names[i] = "$p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            command.Parameters.AddWithValue(names[i], i);
        }

        command.CommandText = "SELECT 1 WHERE 1 IN (" + string.Join(",", names) + ")";
        command.ExecuteScalar();
    }

    /// <summary>
    /// The ceiling the dialect reports is exactly the boundary the engine enforces - not a round
    /// number near it, and not a value inherited from a decade-old default.
    /// </summary>
    [Fact]
    public void TheReportedCeilingIsExactlyWhatTheProviderAccepts()
    {
        int ceiling = new SQLiteDialect().MaxParametersPerStatement;
        Assert.Equal(32766, ceiling);

        using SqliteConnection connection = OpenMemory();

        // At the ceiling: must execute.
        ExecuteInList(connection, ceiling);

        // One over: must not. If this stops throwing, the native limit rose and the dialect is now
        // understating it - the same defect as before, just at a different number.
        SqliteException tooMany = Assert.Throws<SqliteException>(() => ExecuteInList(connection, ceiling + 1));
        Assert.Contains("too many SQL variables", tooMany.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The defect as a caller met it: a thousand ids is an ordinary request, and it was refused.
    /// </summary>
    [Theory]
    [InlineData(1000)]
    [InlineData(1500)]
    [InlineData(5000)]
    [InlineData(20000)]
    public void AnInListTheEngineAcceptsIsNoLongerRefusedByJaunty(int idCount)
    {
        using SqliteConnection connection = OpenMemory();

        using (SqliteCommand create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE probe (Id INTEGER PRIMARY KEY)";
            create.ExecuteNonQuery();
        }

        using (SqliteCommand seed = connection.CreateCommand())
        {
            seed.CommandText = "INSERT INTO probe (Id) VALUES (1), (2), (3)";
            seed.ExecuteNonQuery();
        }

        var ids = new int[idCount];
        for (int i = 0; i < idCount; i++)
            ids[i] = i + 1;

        List<ProbeRow> rows = connection.Query<ProbeRow>(
            "SELECT Id FROM probe WHERE Id IN @Ids", new { Ids = ids }).ToList();

        Assert.Equal(3, rows.Count);
    }

    /// <summary>
    /// The guard still guards. Past the real ceiling Jaunty says so in its own words, rather than
    /// letting the caller decode the provider's "too many SQL variables".
    /// </summary>
    [Fact]
    public void PastTheRealCeilingJauntyStillRefusesWithItsOwnMessage()
    {
        using SqliteConnection connection = OpenMemory();

        var ids = new int[40000];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = i + 1;

        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => connection.Query<ProbeRow>("SELECT Id FROM probe WHERE Id IN @Ids", new { Ids = ids }).ToList());

        Assert.Contains("32766", refused.Message, StringComparison.Ordinal);
        Assert.Contains("batching", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The three dialects that were already right must stay right.</summary>
    [Fact]
    public void TheOtherDialectsKeepTheirDocumentedEngineLimits()
    {
        Assert.Equal(2100, new SqlServerDialect().MaxParametersPerStatement);
        Assert.Equal(65535, new PostgreSqlDialect().MaxParametersPerStatement);
        Assert.Equal(65535, new MySqlDialect().MaxParametersPerStatement);
    }

    private sealed class ProbeRow
    {
        public int Id { get; set; }
    }
}
