using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Interfaces;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-129. All six delete entry points reported <c>options.CommandType</c> to the interceptor
/// pipeline and none of the six <c>*Direct</c> methods ever assigned it, so an auditor was told the
/// command ran as <c>StoredProcedure</c> while it ran as <c>Text</c>. Asking SQLite for
/// <c>CommandType.StoredProcedure</c> is the cheapest observation, as it is for the
/// <c>GetById</c> twin: the provider rejects the assignment, which can only happen if the value
/// reached the command.
/// </summary>
public class DeleteCommandTypeTests
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

    [Table("rows")]
    private sealed class TypedRow : IEntity<int>
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

    [Fact]
    public void DeleteByEntity_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        Assert.ThrowsAny<Exception>(() =>
            connection.Delete(new Row { Id = 1 }, CommandOptions.AsStoredProcedure()));
    }

    [Fact]
    public async Task DeleteByEntityAsync_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.DeleteAsync(new Row { Id = 1 }, CommandOptions.AsStoredProcedure(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DeleteById_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        Assert.ThrowsAny<Exception>(() =>
            connection.Delete<Row>(1, CommandOptions.AsStoredProcedure()));
    }

    [Fact]
    public async Task DeleteByIdAsync_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.DeleteAsync<Row>(1, CommandOptions.AsStoredProcedure(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DeleteByTypedId_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        Assert.ThrowsAny<Exception>(() =>
            connection.Delete<TypedRow, int>(1, CommandOptions.AsStoredProcedure()));
    }

    [Fact]
    public async Task DeleteByTypedIdAsync_CommandType_ReachesTheCommand()
    {
        using SqliteConnection connection = Seed();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.DeleteAsync<TypedRow, int>(1, CommandOptions.AsStoredProcedure(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The other half of the allow-list guard: a <c>default</c> <c>CommandOptions</c> carries
    /// <c>CommandType</c> 0, which providers reject outright, so it must not be assigned.
    /// </summary>
    [Fact]
    public void DefaultCommandType_StillDeletesAsText()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal(1, connection.Delete<Row>(1, default));
    }

    [Fact]
    public async Task DefaultCommandTypeAsync_StillDeletesAsText()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal(1, await connection.DeleteAsync(new Row { Id = 1 }, default, TestContext.Current.CancellationToken));
    }
}
