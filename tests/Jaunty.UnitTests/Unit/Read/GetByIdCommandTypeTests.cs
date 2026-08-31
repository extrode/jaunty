using Jaunty.Attributes;
using Jaunty.Core;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// R27 batch 5 (medium). The four by-id cores reported <c>options.CommandType</c> to the
/// interceptor pipeline but never assigned <c>command.CommandType</c> - the AUD-R26 GetAllCore
/// fix did not bring the by-id siblings along. Asking SQLite for
/// <c>CommandType.StoredProcedure</c> is the cheapest observation: the provider rejects the
/// assignment, which can only happen if the value actually reached the command.
/// </summary>
public class GetByIdCommandTypeTests
{
    [Table("rows")]
    private sealed class Row
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "";
    }

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = "CREATE TABLE rows (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO rows VALUES (1, 'one');";
        seed.ExecuteNonQuery();

        return connection;
    }

    [Fact]
    public void Get_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        Assert.ThrowsAny<Exception>(() =>
            _ = connection.Get<Row>(1, CommandOptions<Row>.AsStoredProcedure()));
    }

    [Fact]
    public async Task GetAsync_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            _ = await connection.GetAsync<Row>(1, CommandOptions<Row>.AsStoredProcedure()));
    }

    [Fact]
    public void Get_DefaultCommandType_StillExecutesAsText()
    {
        using SqliteConnection connection = Seed();

        Row? row = connection.Get<Row>(1, default);

        Assert.NotNull(row);
        Assert.Equal("one", row!.Name);
    }

    [Fact]
    public async Task GetAsync_DefaultCommandType_StillExecutesAsText()
    {
        using SqliteConnection connection = Seed();

        Row? row = await connection.GetAsync<Row>(1, default);

        Assert.NotNull(row);
    }
}
