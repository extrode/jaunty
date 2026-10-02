using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Tests.Helpers.Dialects;

namespace Extrode.Jaunty.Tests.Integration.Write;

/// <summary>
/// An identity key is left out of the INSERT column list, so the ON CONFLICT / ON DUPLICATE KEY
/// upsert never conflicted on it and inserted a new row on every call (found 2026-10-02).
/// </summary>
[Collection("Write Operations")]
public class UpsertGeneratedKeyTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public UpsertGeneratedKeyTests(DialectFixture fixture) => _fixture = fixture;

    private static void Execute(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static long Scalar(IDbConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static string? Name(IDbConnection connection, long id)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT name FROM {UpsertGeneratedKeyEntity.TableName} WHERE id = {id}";
        return cmd.ExecuteScalar() as string;
    }

    private static string Drop(DialectInfo dialect) => dialect.Provider == DialectProvider.SqlServer
        ? $"IF OBJECT_ID('dbo.{UpsertGeneratedKeyEntity.TableName}', 'U') IS NOT NULL DROP TABLE dbo.{UpsertGeneratedKeyEntity.TableName};"
        : $"DROP TABLE IF EXISTS {UpsertGeneratedKeyEntity.TableName};";

    private static string Create(DialectInfo dialect)
    {
        string id = dialect.Provider switch
        {
            DialectProvider.SqlServer => "id INT IDENTITY(1,1) PRIMARY KEY",
            DialectProvider.Postgres => "id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY",
            DialectProvider.MariaDb => "id INT AUTO_INCREMENT PRIMARY KEY",
            _ => "id INTEGER PRIMARY KEY AUTOINCREMENT",
        };
        return $"CREATE TABLE {UpsertGeneratedKeyEntity.TableName} ({id}, name VARCHAR(50) NOT NULL, score INT NULL, note VARCHAR(50) NULL);";
    }

    private IDbConnection Seeded(DialectInfo dialect)
    {
        IDbConnection connection = _fixture.GetConnection(dialect);
        Execute(connection, Drop(dialect));
        Execute(connection, Create(dialect));
        Execute(connection, $"INSERT INTO {UpsertGeneratedKeyEntity.TableName} (name) VALUES ('first')");
        Execute(connection, $"INSERT INTO {UpsertGeneratedKeyEntity.TableName} (name) VALUES ('second')");
        return connection;
    }

    private static long IdOf(IDbConnection connection, string name)
        => Scalar(connection, $"SELECT id FROM {UpsertGeneratedKeyEntity.TableName} WHERE name = '{name}'");

    private static long Count(IDbConnection connection)
        => Scalar(connection, $"SELECT COUNT(*) FROM {UpsertGeneratedKeyEntity.TableName}");

    private static (object Score, object Note) Nullables(IDbConnection connection, long id)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT score, note FROM {UpsertGeneratedKeyEntity.TableName} WHERE id = {id}";
        using IDataReader reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetValue(0), reader.GetValue(1));
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void AnExistingKey_UpdatesThatRow(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            long id = IdOf(connection, "second");

            int affected = connection.Upsert(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "renamed" });

            Assert.Equal(1, affected);
            Assert.Equal(2, Count(connection));
            Assert.Equal("renamed", Name(connection, id));
            Assert.Equal("first", Name(connection, IdOf(connection, "first")));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void AnUnmatchedKey_InsertsARowWhoseKeyTheDatabaseAssigns(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            int affected = connection.Upsert(new UpsertGeneratedKeyEntity { Id = 999, Name = "third" });

            Assert.Equal(1, affected);
            Assert.Equal(3, Count(connection));
            long id = IdOf(connection, "third");
            Assert.NotEqual(999, id);
            Assert.True(id > IdOf(connection, "second"));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void AnUnsetKey_InsertsEachTime(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            connection.Upsert(new UpsertGeneratedKeyEntity { Name = "new" });
            connection.Upsert(new UpsertGeneratedKeyEntity { Name = "new" });

            Assert.Equal(4, Count(connection));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpsertAsync_AnExistingKey_UpdatesThatRow(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            long id = IdOf(connection, "first");

            int affected = await connection.UpsertAsync(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "renamed" }, TestContext.Current.CancellationToken);

            Assert.Equal(1, affected);
            Assert.Equal(2, Count(connection));
            Assert.Equal("renamed", Name(connection, id));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpsertAsync_AnUnmatchedKey_InsertsARow(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            int affected = await connection.UpsertAsync(new UpsertGeneratedKeyEntity { Id = 999, Name = "third" }, TestContext.Current.CancellationToken);

            Assert.Equal(1, affected);
            Assert.Equal(3, Count(connection));
            Assert.NotEqual(999, IdOf(connection, "third"));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    private static IDisposable RecordAutocommitOnRowUpdate(IDbConnection connection, List<bool> autocommit)
    {
        switch (connection)
        {
            case Microsoft.Data.Sqlite.SqliteConnection microsoft:
                SQLitePCL.sqlite3 handle = microsoft.Handle!;
                SQLitePCL.raw.sqlite3_update_hook(handle, (SQLitePCL.delegate_update)((_, _, _, _, _) => autocommit.Add(SQLitePCL.raw.sqlite3_get_autocommit(handle) != 0)), null);
                return new Release(() => SQLitePCL.raw.sqlite3_update_hook(handle, (SQLitePCL.delegate_update?)null, null));
            case System.Data.SQLite.SQLiteConnection system:
                System.Data.SQLite.SQLiteUpdateEventHandler handler = (_, _) => autocommit.Add(system.AutoCommit);
                system.Update += handler;
                return new Release(() => system.Update -= handler);
            default:
                throw new NotSupportedException(connection.GetType().Name);
        }
    }

    private sealed class Release(Action release) : IDisposable
    {
        public void Dispose() => release();
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void SQLite_RunsTheUpdateInsideItsOwnTransaction(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            long id = IdOf(connection, "second");
            var autocommit = new List<bool>();

            using (RecordAutocommitOnRowUpdate(connection, autocommit))
                Assert.Equal(1, connection.Upsert(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "renamed" }));

            Assert.Equal([false], autocommit);
            Assert.Equal("renamed", Name(connection, id));
            Assert.Equal(2, Count(connection));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task SQLite_UpsertAsync_RunsTheUpdateInsideItsOwnTransaction(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            long id = IdOf(connection, "second");
            var autocommit = new List<bool>();

            using (RecordAutocommitOnRowUpdate(connection, autocommit))
                Assert.Equal(1, await connection.UpsertAsync(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "renamed" }, TestContext.Current.CancellationToken));

            Assert.Equal([false], autocommit);
            Assert.Equal("renamed", Name(connection, id));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [MariaDB]
    public async Task MariaDb_RunsTheUpdateInsideItsOwnTransaction(DialectInfo dialect)
    {
        string table = UpsertGeneratedKeyEntity.TableName;
        string log = table + "_txlog";
        using IDbConnection connection = Seeded(dialect);
        try
        {
            Execute(connection, $"CREATE TEMPORARY TABLE {log} (v INT)");
            Execute(connection, $"CREATE TRIGGER {table}_upd AFTER UPDATE ON {table} FOR EACH ROW INSERT INTO {log} VALUES (@@in_transaction)");
            long id = IdOf(connection, "second");

            Assert.Equal(1, connection.Upsert(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "renamed" }));
            Assert.Equal(1, await connection.UpsertAsync(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "again" }, TestContext.Current.CancellationToken));

            Assert.Equal(2, Scalar(connection, $"SELECT COUNT(*) FROM {log}"));
            Assert.Equal(2, Scalar(connection, $"SELECT SUM(v) FROM {log}"));
            Assert.Equal("again", Name(connection, id));
        }
        finally
        {
            Execute(connection, Drop(dialect));
            Execute(connection, $"DROP TEMPORARY TABLE IF EXISTS {log}");
        }
    }

    [Theory]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void ACallerTransaction_IsUsedInsteadOfItsOwn(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            long id = IdOf(connection, "second");

            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                Assert.Equal(1, connection.Upsert(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "renamed" }, new Extrode.Jaunty.Core.CommandOptions(transaction: transaction)));
                transaction.Rollback();
            }

            Assert.Equal("second", Name(connection, id));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void AnOpenTransactionNotPassedIn_RunsInsideIt(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            long id = IdOf(connection, "second");

            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                Assert.Equal(1, connection.Upsert(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "renamed" }));
                transaction.Rollback();
            }

            Assert.Equal("second", Name(connection, id));
            Assert.Equal(2, Count(connection));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [MariaDB]
    [MicrosoftSqlite]
    public async Task AFailedInsert_LeavesNoTransactionOpen(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            Assert.ThrowsAny<System.Data.Common.DbException>(() => connection.Upsert(new UpsertGeneratedKeyEntity { Name = null! }));
            await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => connection.UpsertAsync(new UpsertGeneratedKeyEntity { Name = null! }, TestContext.Current.CancellationToken).AsTask());

            using (IDbTransaction transaction = connection.BeginTransaction())
                transaction.Rollback();

            Assert.Equal(2, Count(connection));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void AFailedUpsertInsideAnOpenTransactionNotPassedIn_KeepsTheCallersWork(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                using (IDbCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = $"INSERT INTO {UpsertGeneratedKeyEntity.TableName} (name) VALUES ('callers')";
                    insert.ExecuteNonQuery();
                }

                Assert.ThrowsAny<System.Data.Common.DbException>(() => connection.Upsert(new UpsertGeneratedKeyEntity { Name = null! }));

                transaction.Commit();
            }

            Assert.Equal(3, Count(connection));
            Assert.Equal(1, Scalar(connection, $"SELECT COUNT(*) FROM {UpsertGeneratedKeyEntity.TableName} WHERE name = 'callers'"));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [MariaDB]
    [SystemSqlite]
    public async Task UpsertAsync_AFailedUpsertInsideAnOpenTransactionNotPassedIn_KeepsTheCallersWork(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            using (IDbTransaction transaction = connection.BeginTransaction())
            {
                using (IDbCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = $"INSERT INTO {UpsertGeneratedKeyEntity.TableName} (name) VALUES ('callers')";
                    insert.ExecuteNonQuery();
                }

                await Assert.ThrowsAnyAsync<System.Data.Common.DbException>(() => connection.UpsertAsync(new UpsertGeneratedKeyEntity { Name = null! }, TestContext.Current.CancellationToken).AsTask());

                transaction.Commit();
            }

            Assert.Equal(3, Count(connection));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }

    [Theory]
    [Postgres]
    public void Postgres_RunsTheUpdateAndTheInsertInOneTransaction(DialectInfo dialect)
    {
        string table = UpsertGeneratedKeyEntity.TableName;
        string log = table + "_txlog";
        string fn = table + "_log_tx";
        using IDbConnection connection = Seeded(dialect);
        try
        {
            Execute(connection, $"""
                CREATE TEMP TABLE {log} (stmt TEXT, tx BIGINT);
                CREATE OR REPLACE FUNCTION pg_temp.{fn}() RETURNS trigger LANGUAGE plpgsql AS $f$
                BEGIN
                    INSERT INTO {log} VALUES (TG_OP, txid_current());
                    RETURN NULL;
                END $f$;
                CREATE TRIGGER {table}_upd BEFORE UPDATE ON {table} FOR EACH STATEMENT EXECUTE FUNCTION pg_temp.{fn}();
                CREATE TRIGGER {table}_ins BEFORE INSERT ON {table} FOR EACH STATEMENT EXECUTE FUNCTION pg_temp.{fn}();
                """);
            long id = IdOf(connection, "second");

            Assert.Equal(1, connection.Upsert(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "renamed" }));

            Assert.Equal(2, Scalar(connection, $"SELECT COUNT(*) FROM {log}"));
            Assert.Equal(1, Scalar(connection, $"SELECT COUNT(DISTINCT tx) FROM {log}"));
        }
        finally
        {
            Execute(connection, Drop(dialect));
            Execute(connection, $"DROP TABLE IF EXISTS {log}");
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void NullAndIntegerValues_InsertThenUpdate(DialectInfo dialect)
    {
        using IDbConnection connection = Seeded(dialect);
        try
        {
            Assert.Equal(1, connection.Upsert(new UpsertGeneratedKeyEntity { Name = "nulls", Score = null, Note = null }));
            long id = IdOf(connection, "nulls");
            Assert.Equal((DBNull.Value, DBNull.Value), Nullables(connection, id));

            Assert.Equal(1, connection.Upsert(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "nulls", Score = 7, Note = "set" }));
            (object score, object note) = Nullables(connection, id);
            Assert.Equal(7L, Convert.ToInt64(score));
            Assert.Equal("set", note);

            Assert.Equal(1, connection.Upsert(new UpsertGeneratedKeyEntity { Id = (int)id, Name = "nulls", Score = null, Note = null }));
            Assert.Equal((DBNull.Value, DBNull.Value), Nullables(connection, id));
            Assert.Equal(3, Count(connection));
        }
        finally
        {
            Execute(connection, Drop(dialect));
        }
    }
}

[Table(TableName)]
public class UpsertGeneratedKeyEntity
{
#if NET10_0_OR_GREATER
    public const string TableName = "upsert_generated_key_n10";
#elif NET8_0_OR_GREATER
    public const string TableName = "upsert_generated_key_n8";
#else
    public const string TableName = "upsert_generated_key_fx";
#endif

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("score")]
    public int? Score { get; set; }

    [Column("note")]
    public string? Note { get; set; }
}
