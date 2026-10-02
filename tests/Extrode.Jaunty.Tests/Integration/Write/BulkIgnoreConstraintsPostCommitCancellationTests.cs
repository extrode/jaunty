using Microsoft.Data.Sqlite;

using SQLitePCL;

namespace Extrode.Jaunty.Tests.Integration.Write;

/// <summary>
/// AUD-R38-062..064: on SQLite the foreign-key re-enable runs after the commit, and it ran with the
/// caller's token, so a cancellation landing between the two reported committed rows as cancelled.
/// SQLite's commit hook cancels the token at exactly that point.
/// </summary>
public class BulkIgnoreConstraintsPostCommitCancellationTests
{
    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var create = connection.CreateCommand();
        create.CommandText =
            "PRAGMA foreign_keys = ON;" +
            "CREATE TABLE fk_parent (id INTEGER PRIMARY KEY, name TEXT);" +
            "CREATE TABLE fk_child (id INTEGER PRIMARY KEY, parent_id INTEGER, FOREIGN KEY(parent_id) REFERENCES fk_parent(id));" +
            "INSERT INTO fk_parent (id, name) VALUES (1, 'Parent');" +
            "INSERT INTO fk_child (id, parent_id) VALUES (1, 1);";
        create.ExecuteNonQuery();
        return connection;
    }

    private static void CancelOnCommit(SqliteConnection connection, CancellationTokenSource cts)
        => raw.sqlite3_commit_hook(connection.Handle, _ =>
        {
            cts.Cancel();
            return 0;
        }, null);

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public async Task BulkDeleteIgnoreConstraintsAsync_CancelledAfterCommit_ReportsTheCommittedRows()
    {
        using SqliteConnection connection = Open();
        using var cts = new CancellationTokenSource();
        CancelOnCommit(connection, cts);

        int deleted = await connection.BulkDeleteIgnoreConstraintsAsync(new List<FkParent> { new() { Id = 1 } }, cts.Token);

        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(1, deleted);
        Assert.Equal(0L, Scalar(connection, "SELECT COUNT(*) FROM fk_parent"));
        Assert.Equal(1L, Scalar(connection, "PRAGMA foreign_keys"));
    }

    [Fact]
    public async Task BulkInsertIgnoreConstraintsAsync_CancelledAfterCommit_ReportsTheCommittedRows()
    {
        using SqliteConnection connection = Open();
        using var cts = new CancellationTokenSource();
        CancelOnCommit(connection, cts);

        int inserted = await connection.BulkInsertIgnoreConstraintsAsync(new List<FkChild> { new() { Id = 2, ParentId = 999 } }, cts.Token);

        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(1, inserted);
        Assert.Equal(2L, Scalar(connection, "SELECT COUNT(*) FROM fk_child"));
        Assert.Equal(1L, Scalar(connection, "PRAGMA foreign_keys"));
    }

    [Fact]
    public async Task BulkUpdateIgnoreConstraintsAsync_CancelledAfterCommit_ReportsTheCommittedRows()
    {
        using SqliteConnection connection = Open();
        using var cts = new CancellationTokenSource();
        CancelOnCommit(connection, cts);

        int updated = await connection.BulkUpdateIgnoreConstraintsAsync(new List<FkChild> { new() { Id = 1, ParentId = 999 } }, cts.Token);

        Assert.True(cts.IsCancellationRequested);
        Assert.Equal(1, updated);
        Assert.Equal(999L, Scalar(connection, "SELECT parent_id FROM fk_child WHERE id = 1"));
        Assert.Equal(1L, Scalar(connection, "PRAGMA foreign_keys"));
    }
}
