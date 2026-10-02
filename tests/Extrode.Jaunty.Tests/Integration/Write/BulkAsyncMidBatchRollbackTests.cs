using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Core;

using Microsoft.Data.Sqlite;

namespace Extrode.Jaunty.Tests.Integration.Write;

/// <summary>
/// coverage-gaps-2026-09-20: <c>BulkDeleteAsync</c>/<c>BulkUpdateAsync</c>'s own-transaction
/// <c>RollbackAsync</c> was tested only for a failure <i>before</i> <c>BeginTransactionAsync</c>
/// (<see cref="BulkIgnoreConstraintsFailureRestoresForeignKeysTests"/>), where the transaction is
/// still null and the guarded rollback call never runs. These fail on the second row of a
/// two-row batch, after a real transaction has already opened, so the actual
/// <c>transaction.RollbackAsync(...)</c> call executes.
/// </summary>
public class BulkAsyncMidBatchRollbackTests
{
    [Table("mbr_parent")]
    public class MbrParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    [Table("mbr_child")]
    public class MbrChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("parent_id")]
        public int ParentId { get; set; }
    }

    [Table("mbr_item")]
    public class MbrItem
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    private static SqliteConnection CreateSqliteWithForeignKeyFixture()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON";
        pragma.ExecuteNonQuery();

        using SqliteCommand create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE mbr_parent (id INTEGER PRIMARY KEY, name TEXT);" +
            "CREATE TABLE mbr_child (id INTEGER PRIMARY KEY, parent_id INTEGER, FOREIGN KEY(parent_id) REFERENCES mbr_parent(id));" +
            "INSERT INTO mbr_parent (id, name) VALUES (1, 'no-children'), (2, 'has-children');" +
            "INSERT INTO mbr_child (id, parent_id) VALUES (100, 2);";
        create.ExecuteNonQuery();

        return connection;
    }

    private static SqliteConnection CreateSqliteWithUniqueFixture()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE mbr_item (id INTEGER PRIMARY KEY, name TEXT UNIQUE);" +
            "INSERT INTO mbr_item (id, name) VALUES (1, 'a'), (2, 'b'), (3, 'c');";
        create.ExecuteNonQuery();

        return connection;
    }

    /// <summary>
    /// Row 1 (no children) deletes cleanly; row 2 (referenced by a child) violates the foreign key
    /// on the second <c>ExecuteNonQueryAsync</c> call, after the transaction already opened.
    /// </summary>
    [Fact]
    public async Task BulkDeleteAsync_FailureOnTheSecondRow_RollsBackTheFirstRowsDelete()
    {
        using SqliteConnection connection = CreateSqliteWithForeignKeyFixture();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            connection.BulkDeleteAsync(
                new List<MbrParent> { new() { Id = 1 }, new() { Id = 2 } },
                TestContext.Current.CancellationToken).AsTask());

        using SqliteCommand count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM mbr_parent";
        long remaining = (long)(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;

        Assert.Equal(2, remaining);
    }

    /// <summary>
    /// Row 1's rename succeeds; row 2 is renamed to row 3's existing name, violating the unique
    /// index on the second <c>ExecuteNonQueryAsync</c> call, after the transaction already opened.
    /// </summary>
    [Fact]
    public async Task BulkUpdateAsync_FailureOnTheSecondRow_RollsBackTheFirstRowsUpdate()
    {
        using SqliteConnection connection = CreateSqliteWithUniqueFixture();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            connection.BulkUpdateAsync(
                new List<MbrItem>
                {
                    new() { Id = 1, Name = "a-renamed" },
                    new() { Id = 2, Name = "c" },
                },
                TestContext.Current.CancellationToken).AsTask());

        using SqliteCommand check = connection.CreateCommand();
        check.CommandText = "SELECT name FROM mbr_item WHERE id = 1";
        object? name = await check.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        Assert.Equal("a", name);
    }
}
