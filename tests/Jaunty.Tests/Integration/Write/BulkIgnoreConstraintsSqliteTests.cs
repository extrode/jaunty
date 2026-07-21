using System.Data.Common;

using Jaunty.Attributes;
using Jaunty.Core;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Integration.Write;

[Table("fk_parent")]
public partial class FkParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

[Table("fk_child")]
public partial class FkChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

/// <summary>
/// Regression tests proving that Bulk*IgnoreConstraints actually bypasses foreign key
/// enforcement on SQLite, where PRAGMA foreign_keys can only be toggled in autocommit mode.
/// </summary>
public class BulkIgnoreConstraintsSqliteTests
{
    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        InitializeSchema(connection);
        return connection;
    }

    private static void InitializeSchema(SqliteConnection connection)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON";
        pragma.ExecuteNonQuery();

        using var create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE fk_parent (id INTEGER PRIMARY KEY, name TEXT);" +
            "CREATE TABLE fk_child (id INTEGER PRIMARY KEY, parent_id INTEGER, FOREIGN KEY(parent_id) REFERENCES fk_parent(id));";
        create.ExecuteNonQuery();
    }

    private static void SeedParentWithChild(SqliteConnection connection)
    {
        using var seed = connection.CreateCommand();
        seed.CommandText =
            "INSERT INTO fk_parent (id, name) VALUES (1, 'Parent');" +
            "INSERT INTO fk_child (id, parent_id) VALUES (1, 1);";
        seed.ExecuteNonQuery();
    }

    private static int CountRows(SqliteConnection connection, string table)
    {
        using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(count.ExecuteScalar());
    }

    [Fact]
    public void BulkDeleteIgnoreConstraints_ParentWithChildren_Succeeds()
    {
        using var connection = CreateConnection();
        SeedParentWithChild(connection);

        var parents = new List<FkParent> { new() { Id = 1 } };

        int deleted = connection.BulkDeleteIgnoreConstraints(parents);

        Assert.Equal(1, deleted);
        Assert.Equal(0, CountRows(connection, "fk_parent"));
        Assert.Equal(1, CountRows(connection, "fk_child"));
    }

    [Fact]
    public void BulkDelete_ParentWithChildren_ThrowsOnForeignKeyViolation()
    {
        using var connection = CreateConnection();
        SeedParentWithChild(connection);

        var parents = new List<FkParent> { new() { Id = 1 } };

        Assert.ThrowsAny<DbException>(() => connection.BulkDelete(parents));
        Assert.Equal(1, CountRows(connection, "fk_parent"));
    }

    [Fact]
    public void BulkInsertIgnoreConstraints_ChildWithMissingParent_Succeeds()
    {
        using var connection = CreateConnection();

        var children = new List<FkChild> { new() { Id = 1, ParentId = 999 } };

        int inserted = connection.BulkInsertIgnoreConstraints(children);

        Assert.Equal(1, inserted);
        Assert.Equal(1, CountRows(connection, "fk_child"));
    }

    [Fact]
    public void BulkInsert_ChildWithMissingParent_ThrowsOnForeignKeyViolation()
    {
        using var connection = CreateConnection();

        var children = new List<FkChild> { new() { Id = 1, ParentId = 999 } };

        Assert.ThrowsAny<DbException>(() => connection.BulkInsert(children));
        Assert.Equal(0, CountRows(connection, "fk_child"));
    }

    [Fact]
    public void BulkUpdateIgnoreConstraints_ChildToMissingParent_Succeeds()
    {
        using var connection = CreateConnection();
        SeedParentWithChild(connection);

        var children = new List<FkChild> { new() { Id = 1, ParentId = 999 } };

        int updated = connection.BulkUpdateIgnoreConstraints(children);

        Assert.Equal(1, updated);
        Assert.Equal(1, CountRows(connection, "fk_child"));
    }

    [Fact]
    public void BulkDeleteIgnoreConstraints_WithCallerTransaction_ThrowsNotSupported()
    {
        using var connection = CreateConnection();
        SeedParentWithChild(connection);

        var parents = new List<FkParent> { new() { Id = 1 } };

        using var transaction = connection.BeginTransaction();

        Assert.Throws<NotSupportedException>(() =>
            connection.BulkDeleteIgnoreConstraints(parents, new CommandOptions(transaction: transaction)));
    }

    [Fact]
    public void BulkInsertIgnoreConstraints_WithCallerTransaction_ThrowsNotSupported()
    {
        using var connection = CreateConnection();

        var children = new List<FkChild> { new() { Id = 1, ParentId = 999 } };

        using var transaction = connection.BeginTransaction();

        Assert.Throws<NotSupportedException>(() =>
            connection.BulkInsertIgnoreConstraints(children, new CommandOptions(transaction: transaction)));
    }

    [Fact]
    public async Task BulkDeleteIgnoreConstraintsAsync_ParentWithChildren_Succeeds()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        InitializeSchema(connection);
        SeedParentWithChild(connection);

        var parents = new List<FkParent> { new() { Id = 1 } };

        int deleted = await connection.BulkDeleteIgnoreConstraintsAsync(parents);

        Assert.Equal(1, deleted);
        Assert.Equal(0, CountRows(connection, "fk_parent"));
        Assert.Equal(1, CountRows(connection, "fk_child"));
    }

    [Fact]
    public async Task BulkInsertIgnoreConstraintsAsync_ChildWithMissingParent_Succeeds()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        InitializeSchema(connection);

        var children = new List<FkChild> { new() { Id = 1, ParentId = 999 } };

        int inserted = await connection.BulkInsertIgnoreConstraintsAsync(children);

        Assert.Equal(1, inserted);
        Assert.Equal(1, CountRows(connection, "fk_child"));
    }

    [Fact]
    public async Task BulkInsertAsync_ChildWithMissingParent_ThrowsOnForeignKeyViolation()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        InitializeSchema(connection);

        var children = new List<FkChild> { new() { Id = 1, ParentId = 999 } };

        await Assert.ThrowsAnyAsync<DbException>(() => connection.BulkInsertAsync(children).AsTask());
        Assert.Equal(0, CountRows(connection, "fk_child"));
    }

    [Fact]
    public async Task BulkInsertIgnoreConstraintsAsync_WithCallerTransaction_ThrowsNotSupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        InitializeSchema(connection);

        var children = new List<FkChild> { new() { Id = 1, ParentId = 999 } };

        using var transaction = connection.BeginTransaction();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            connection.BulkInsertIgnoreConstraintsAsync(children, CommandOptions.WithTransaction(transaction)).AsTask());
    }
}
