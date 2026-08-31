using Jaunty.Attributes;
using Jaunty.Core;
using Jaunty.Interfaces;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Write;

/// <summary>
/// AUD-R35-133. The identity write-back was gated on <c>id &gt; 0</c>, so an insert that generated a
/// negative key returned that key to the caller and left the entity's id property at its default.
/// SQLite makes the case reachable directly: for an <c>INTEGER PRIMARY KEY</c> it assigns
/// <c>max(rowid) + 1</c>, so a table seeded with a negative rowid hands out negative keys - the same
/// shape as SQL Server's <c>IDENTITY(-2147483648, 1)</c> wide-range seed.
/// </summary>
public class InsertIdentityWriteBackTests
{
    [Table("negative_identity_rows")]
    public class NegativeIdentityRow : IEntity<int>
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    private static SqliteConnection SeededAt(long seedId)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText =
            "CREATE TABLE negative_identity_rows (id INTEGER PRIMARY KEY, name TEXT);" +
            $"INSERT INTO negative_identity_rows (id, name) VALUES ({seedId}, 'seed');";
        seed.ExecuteNonQuery();

        return connection;
    }

    private static long IdOf(SqliteConnection connection, string name)
    {
        using SqliteCommand read = connection.CreateCommand();
        read.CommandText = $"SELECT id FROM negative_identity_rows WHERE name = '{name}';";
        return (long)read.ExecuteScalar()!;
    }

    [Fact]
    public void ANegativeGeneratedKey_IsWrittenBackToTheEntity()
    {
        using SqliteConnection connection = SeededAt(-5);
        var entity = new NegativeIdentityRow { Name = "row" };

        long returned = connection.Insert(entity);

        Assert.Equal(IdOf(connection, "row"), returned);
        Assert.True(returned < 0, $"expected a negative generated key, got {returned}");
        Assert.Equal(returned, entity.Id);
    }

    [Fact]
    public async Task ANegativeGeneratedKeyAsync_IsWrittenBackToTheEntity()
    {
        using SqliteConnection connection = SeededAt(-5);
        var entity = new NegativeIdentityRow { Name = "row" };

        long returned = await connection.InsertAsync(entity, default, TestContext.Current.CancellationToken);

        Assert.Equal(IdOf(connection, "row"), returned);
        Assert.True(returned < 0, $"expected a negative generated key, got {returned}");
        Assert.Equal(returned, entity.Id);
    }

    /// <summary>
    /// The control that the positive case never regressed, and that the returned value and the
    /// entity's property stay the same number.
    /// </summary>
    [Fact]
    public void APositiveGeneratedKey_IsStillWrittenBack()
    {
        using SqliteConnection connection = SeededAt(10);
        var entity = new NegativeIdentityRow { Name = "row" };

        long returned = connection.Insert(entity);

        Assert.Equal(11, returned);
        Assert.Equal(11, entity.Id);
    }
}
