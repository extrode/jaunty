using Jaunty.Attributes;
using Jaunty.Core;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-129's remainder. <c>DeleteCore</c>'s six <c>*Direct</c> methods were fixed to assign
/// <c>options.CommandType</c>; <c>InsertCore</c> and <c>UpdateCore</c> were named as out of that
/// finding's scope and left reporting the value to the interceptor pipeline while running the
/// generated statement as <c>Text</c>. Same observation as <c>DeleteCommandTypeTests</c>: SQLite
/// rejects <c>CommandType.StoredProcedure</c> outright, so the rejection can only happen if the
/// value reached the command.
/// </summary>
public class InsertUpdateCommandTypeTests
{
    [Table("rows")]
    private sealed class Row
    {
        [Key]
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

    private static string? ScalarString(SqliteConnection connection, string sql)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar() as string;
    }

    private static void AssertRejectedByTheProvider(Exception ex) =>
        Assert.Contains("CommandType", ex.Message, StringComparison.Ordinal);

    [Fact]
    public void Insert_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        var ex = Assert.ThrowsAny<Exception>(() =>
            connection.Insert(new Row { Name = "two" }, CommandOptions.AsStoredProcedure()));

        AssertRejectedByTheProvider(ex);
    }

    [Fact]
    public async Task InsertAsync_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.InsertAsync(new Row { Name = "two" }, CommandOptions.AsStoredProcedure(), TestContext.Current.CancellationToken));

        AssertRejectedByTheProvider(ex);
    }

    [Fact]
    public void Update_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        var ex = Assert.ThrowsAny<Exception>(() =>
            connection.Update(new Row { Id = 1, Name = "renamed" }, CommandOptions.AsStoredProcedure()));

        AssertRejectedByTheProvider(ex);
    }

    [Fact]
    public async Task UpdateAsync_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.UpdateAsync(new Row { Id = 1, Name = "renamed" }, CommandOptions.AsStoredProcedure(), TestContext.Current.CancellationToken));

        AssertRejectedByTheProvider(ex);
    }

    [Fact]
    public void Insert_DefaultOptions_StillRuns()
    {
        using SqliteConnection connection = Seed();

        // Row's key is [Key] without [Identity], so InsertCore takes the ExecuteNonQuery branch
        // and returns rows affected rather than a generated id.
        long affected = connection.Insert(new Row { Id = 2, Name = "two" });

        Assert.Equal(1, affected);
        Assert.Equal("two", ScalarString(connection, "SELECT name FROM rows WHERE id = 2"));
    }

    [Fact]
    public void Update_DefaultOptions_StillRuns()
    {
        using SqliteConnection connection = Seed();

        int affected = connection.Update(new Row { Id = 1, Name = "renamed" });

        Assert.Equal(1, affected);
        Assert.Equal("renamed", ScalarString(connection, "SELECT name FROM rows WHERE id = 1"));
    }
}
