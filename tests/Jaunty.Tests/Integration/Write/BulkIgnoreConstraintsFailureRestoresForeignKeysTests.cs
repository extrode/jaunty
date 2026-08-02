using System.Data;
using System.Data.Common;

using Microsoft.Data.Sqlite;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// AUD-R34-008. In all six bulk paths the foreign-key disable calls, and the
/// <c>BeginTransaction</c> between them, sat <b>outside</b> the inner <c>try</c> whose <c>catch</c>
/// re-enables them; the outer <c>finally</c> only disposes the transaction and closes the
/// connection. So a throw from <c>BeginTransaction</c>, or from the second disable, left foreign
/// key enforcement <b>off</b> - and on a pooled connection it stays off for every later caller that
/// borrows it, which is a silent integrity hole rather than a failed operation.
/// <para>
/// SQLite is the engine that makes this reachable and observable: it is the one dialect with
/// <c>RequiresAutocommitForForeignKeyToggle</c>, so the first <c>PRAGMA foreign_keys = OFF</c> runs
/// before the transaction is opened, and <c>PRAGMA foreign_keys</c> reads the state straight back.
/// The connection wrapper below fails <c>BeginTransaction</c> and nothing else, so the operation is
/// interrupted at exactly the point the disable has taken effect and no cleanup path covers it.
/// </para>
/// </summary>
public class BulkIgnoreConstraintsFailureRestoresForeignKeysTests
{
    /// <summary>
    /// Delegates everything to a real SQLite connection except <c>BeginTransaction</c>, which
    /// throws. <c>InnerConnection</c> is public because that is one of the property names
    /// <c>SqlDialectFactory</c> unwraps decorators by, so the dialect still resolves to SQLite.
    /// </summary>
    private sealed class TransactionRefusingConnection : DbConnection
    {
        private readonly SqliteConnection _inner;

        public TransactionRefusingConnection(SqliteConnection inner) => _inner = inner;

        public IDbConnection InnerConnection => _inner;

#pragma warning disable CS8765
        public override string ConnectionString
        {
            get => _inner.ConnectionString;
            set => _inner.ConnectionString = value;
        }
#pragma warning restore CS8765

        public override string Database => _inner.Database;
        public override string DataSource => _inner.DataSource;
        public override string ServerVersion => _inner.ServerVersion;
        public override ConnectionState State => _inner.State;

        public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public override void Close() => _inner.Close();
        public override void Open() => _inner.Open();

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new InvalidOperationException("BeginTransaction refused by the test.");

        protected override DbCommand CreateDbCommand() => _inner.CreateCommand();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private static SqliteConnection CreateSqlite()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON";
        pragma.ExecuteNonQuery();

        using SqliteCommand create = connection.CreateCommand();
        create.CommandText =
            "CREATE TABLE fk_parent (id INTEGER PRIMARY KEY, name TEXT);" +
            "CREATE TABLE fk_child (id INTEGER PRIMARY KEY, parent_id INTEGER, FOREIGN KEY(parent_id) REFERENCES fk_parent(id));";
        create.ExecuteNonQuery();

        return connection;
    }

    private static bool ForeignKeysEnabled(DbConnection connection)
    {
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys";
        return Convert.ToInt32(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 1;
    }

    private static void AssertRestored(DbConnection connection, string operation)
    {
        Assert.True(ForeignKeysEnabled(connection),
            $"{operation} failed with foreign key enforcement still disabled. The connection is " +
            "returned to the pool in that state, so every later borrower writes unvalidated rows.");
    }

    [Fact]
    public void BulkDeleteIgnoreConstraints_WhenBeginTransactionThrows_ReEnablesForeignKeys()
    {
        using SqliteConnection sqlite = CreateSqlite();
        using var connection = new TransactionRefusingConnection(sqlite);

        Assert.ThrowsAny<Exception>(() =>
            connection.BulkDeleteIgnoreConstraints(new List<FkParent> { new() { Id = 1 } }));

        AssertRestored(connection, "BulkDeleteIgnoreConstraints");
    }

    [Fact]
    public void BulkInsertIgnoreConstraints_WhenBeginTransactionThrows_ReEnablesForeignKeys()
    {
        using SqliteConnection sqlite = CreateSqlite();
        using var connection = new TransactionRefusingConnection(sqlite);

        Assert.ThrowsAny<Exception>(() =>
            connection.BulkInsertIgnoreConstraints(new List<FkChild> { new() { Id = 1, ParentId = 99 } }));

        AssertRestored(connection, "BulkInsertIgnoreConstraints");
    }

    [Fact]
    public void BulkUpdateIgnoreConstraints_WhenBeginTransactionThrows_ReEnablesForeignKeys()
    {
        using SqliteConnection sqlite = CreateSqlite();
        using var connection = new TransactionRefusingConnection(sqlite);

        Assert.ThrowsAny<Exception>(() =>
            connection.BulkUpdateIgnoreConstraints(new List<FkChild> { new() { Id = 1, ParentId = 99 } }));

        AssertRestored(connection, "BulkUpdateIgnoreConstraints");
    }

    [Fact]
    public async Task BulkDeleteIgnoreConstraintsAsync_WhenBeginTransactionThrows_ReEnablesForeignKeys()
    {
        using SqliteConnection sqlite = CreateSqlite();
        using var connection = new TransactionRefusingConnection(sqlite);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.BulkDeleteIgnoreConstraintsAsync(new List<FkParent> { new() { Id = 1 } }));

        AssertRestored(connection, "BulkDeleteIgnoreConstraintsAsync");
    }

    [Fact]
    public async Task BulkInsertIgnoreConstraintsAsync_WhenBeginTransactionThrows_ReEnablesForeignKeys()
    {
        using SqliteConnection sqlite = CreateSqlite();
        using var connection = new TransactionRefusingConnection(sqlite);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.BulkInsertIgnoreConstraintsAsync(new List<FkChild> { new() { Id = 1, ParentId = 99 } }));

        AssertRestored(connection, "BulkInsertIgnoreConstraintsAsync");
    }

    [Fact]
    public async Task BulkUpdateIgnoreConstraintsAsync_WhenBeginTransactionThrows_ReEnablesForeignKeys()
    {
        using SqliteConnection sqlite = CreateSqlite();
        using var connection = new TransactionRefusingConnection(sqlite);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await connection.BulkUpdateIgnoreConstraintsAsync(new List<FkChild> { new() { Id = 1, ParentId = 99 } }));

        AssertRestored(connection, "BulkUpdateIgnoreConstraintsAsync");
    }
}
