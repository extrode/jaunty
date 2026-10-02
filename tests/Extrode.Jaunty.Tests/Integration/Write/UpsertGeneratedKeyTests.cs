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
        return $"CREATE TABLE {UpsertGeneratedKeyEntity.TableName} ({id}, name VARCHAR(50) NOT NULL);";
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
}
