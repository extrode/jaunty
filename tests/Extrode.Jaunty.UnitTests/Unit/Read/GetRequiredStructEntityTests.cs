using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.Interfaces;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R40-001. <c>GetRequired</c> detected a missing row with <c>?? throw</c> on the core's result,
/// but for a struct entity the not-found sentinel is <c>default(T)</c>, which is never null, so a
/// missing key returned an all-default entity instead of throwing.
/// </summary>
public class GetRequiredStructEntityTests
{
    private struct StructRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; }
    }

    private struct TypedStructRow : IEntity<int>
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; }
    }

    private static SqliteConnection Seed()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText = "CREATE TABLE StructRow (id INTEGER PRIMARY KEY, name TEXT); CREATE TABLE TypedStructRow (id INTEGER PRIMARY KEY, name TEXT); INSERT INTO StructRow VALUES (1, 'one');";
        seed.ExecuteNonQuery();

        return connection;
    }

    [Fact]
    public void GetRequired_MissingStructEntity_Throws()
    {
        using SqliteConnection connection = Seed();

        var ex = Assert.Throws<InvalidOperationException>(() => connection.GetRequired<StructRow>(99));
        Assert.Contains("not found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetRequired_WithOptions_MissingStructEntity_Throws()
    {
        using SqliteConnection connection = Seed();

        Assert.Throws<InvalidOperationException>(() => connection.GetRequired<StructRow>(99, default(CommandOptions<StructRow>)));
    }

    [Fact]
    public void GetRequired_TypedKey_MissingStructEntity_Throws()
    {
        using SqliteConnection connection = Seed();

        Assert.Throws<InvalidOperationException>(() => connection.GetRequired<TypedStructRow, int>(99));
        Assert.Throws<InvalidOperationException>(() => connection.GetRequired<TypedStructRow, int>(99, default(CommandOptions<TypedStructRow>)));
    }

    [Fact]
    public async Task GetRequiredAsync_MissingStructEntity_Throws()
    {
        using SqliteConnection connection = Seed();
        CancellationToken ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.GetRequiredAsync<StructRow>(99, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.GetRequiredAsync<StructRow>(99, default(CommandOptions<StructRow>), ct));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.GetRequiredAsync<TypedStructRow, int>(99, ct));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await connection.GetRequiredAsync<TypedStructRow, int>(99, default(CommandOptions<TypedStructRow>), ct));
    }

    [Fact]
    public async Task Get_MissingStructEntity_StillReturnsDefault()
    {
        using SqliteConnection connection = Seed();

        Assert.Equal(default, connection.Get<StructRow>(99));
        Assert.Equal(default, await connection.GetAsync<StructRow>(99, TestContext.Current.CancellationToken));
    }
}
