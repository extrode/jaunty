using FluentAssertions;

using Jaunty.Attributes;
using Jaunty.Tests.Helpers.Dialects;

namespace Jaunty.Tests.Integration.Write;

/// <summary>
/// Tests Upsert and UpsertAsync against a dedicated non-identity test table.
/// </summary>
public class UpsertTests : IClassFixture<DialectFixture>
{
    private readonly DialectFixture _fixture;

    public UpsertTests(DialectFixture fixture)
    {
        _fixture = fixture;
    }

    private static void ExecuteSql(IDbConnection connection, string sql, object? parameters = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        if (parameters != null)
        {
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }

        cmd.ExecuteNonQuery();
    }

    private static string? QueryScalarString(IDbConnection connection, string sql, object? parameters = null)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        if (parameters != null)
        {
            foreach (var prop in parameters.GetType().GetProperties())
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + prop.Name;
                param.Value = prop.GetValue(parameters) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        }

        return cmd.ExecuteScalar()?.ToString();
    }

    private static void RecreateUpsertTable(IDbConnection connection, DialectInfo dialect)
    {
        ExecuteSql(connection, DropUpsertTableSql(dialect));
        ExecuteSql(connection, CreateUpsertTableSql(dialect));
    }

    private static string DropUpsertTableSql(DialectInfo dialect) => dialect.Provider switch
    {
        DialectProvider.SqlServer => "IF OBJECT_ID('dbo.upsert_test', 'U') IS NOT NULL DROP TABLE dbo.upsert_test;",
        _ => "DROP TABLE IF EXISTS upsert_test;"
    };

    private static string CreateUpsertTableSql(DialectInfo dialect) => dialect.Provider switch
    {
        DialectProvider.SqlServer =>
            "CREATE TABLE dbo.upsert_test (id INT NOT NULL PRIMARY KEY, name VARCHAR(100) NOT NULL, description VARCHAR(255) NULL);",
        _ =>
            "CREATE TABLE upsert_test (id INT NOT NULL PRIMARY KEY, name VARCHAR(100) NOT NULL, description VARCHAR(255) NULL);"
    };

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Upsert_NewEntity_InsertsRecord(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);
        try
        {
            RecreateUpsertTable(connection, dialect);

            var entity = new UpsertTestEntity
            {
                Id = 1001,
                Name = "UpsertTest",
                Description = "Test category for upsert"
            };

            var result = connection.Upsert(entity);

            if (dialect.Provider == DialectProvider.MariaDb)
                result.Should().BeGreaterThan(0);
            else
                result.Should().Be(1);

            var insertedName = QueryScalarString(connection,
                "SELECT name FROM upsert_test WHERE id = @id",
                new { id = entity.Id });

            insertedName.Should().Be("UpsertTest");
        }
        finally
        {
            ExecuteSql(connection, DropUpsertTableSql(dialect));
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public void Upsert_ExistingEntity_UpdatesRecord(DialectInfo dialect)
    {
        using var connection = _fixture.GetConnection(dialect);

        try
        {
            RecreateUpsertTable(connection, dialect);

            ExecuteSql(connection,
                "INSERT INTO upsert_test (id, name, description) VALUES (@id, @name, @desc)",
                new { id = 1111, name = "OriginalName", desc = "Original description" });

            var entity = new UpsertTestEntity
            {
                Id = 1111,
                Name = "UpdatedName",
                Description = "Updated description"
            };

            var result = connection.Upsert(entity);

            if (dialect.Provider == DialectProvider.MariaDb)
                result.Should().BeGreaterThan(0);
            else
                result.Should().Be(1);

            var updatedName = QueryScalarString(connection,
                "SELECT name FROM upsert_test WHERE id = @id",
                new { id = entity.Id });

            updatedName.Should().Be("UpdatedName");
        }
        finally
        {
            ExecuteSql(connection, DropUpsertTableSql(dialect));
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpsertAsync_NewEntity_InsertsRecord(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        try
        {
            RecreateUpsertTable(connection, dialect);

            var entity = new UpsertTestEntity
            {
                Id = 1003,
                Name = "AsyncUpsertTest",
                Description = "Async test category"
            };

            var result = await connection.UpsertAsync(entity, cancellationToken: CancellationToken.None);

            if (dialect.Provider == DialectProvider.MariaDb)
                result.Should().BeGreaterThan(0);
            else
                result.Should().Be(1);

            var insertedName = QueryScalarString(connection,
                "SELECT name FROM upsert_test WHERE id = @id",
                new { id = entity.Id });

            insertedName.Should().Be("AsyncUpsertTest");
        }
        finally
        {
            ExecuteSql(connection, DropUpsertTableSql(dialect));
        }
    }

    [Theory]
    [SqlServer]
    [Postgres]
    [MariaDB]
    [MicrosoftSqlite]
    [SystemSqlite]
    public async Task UpsertAsync_ExistingEntity_UpdatesRecord(DialectInfo dialect)
    {
        using var connection = _fixture.GetDbConnection(dialect);
        try
        {
            RecreateUpsertTable(connection, dialect);

            ExecuteSql(connection,
                "INSERT INTO upsert_test (id, name, description) VALUES (@id, @name, @desc)",
                new { id = 1004, name = "AsyncOriginal", desc = "Async original" });

            var entity = new UpsertTestEntity
            {
                Id = 1004,
                Name = "AsyncUpdated",
                Description = "Async updated"
            };

            var result = await connection.UpsertAsync(entity, cancellationToken: CancellationToken.None);

            if (dialect.Provider == DialectProvider.MariaDb)
                result.Should().BeGreaterThan(0);
            else
                result.Should().Be(1);

            var updatedName = QueryScalarString(connection,
                "SELECT name FROM upsert_test WHERE id = @id",
                new { id = entity.Id });

            updatedName.Should().Be("AsyncUpdated");
        }
        finally
        {
            ExecuteSql(connection, DropUpsertTableSql(dialect));
        }
    }
}

[Table("upsert_test")]
public class UpsertTestEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }
}
