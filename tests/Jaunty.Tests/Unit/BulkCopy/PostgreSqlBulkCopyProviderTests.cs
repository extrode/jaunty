#if NET8_0_OR_GREATER
using System.Data;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection.BulkCopy;
using Jaunty.Tests.Helpers;

using Npgsql;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// Integration tests for PostgreSqlBulkCopyProvider (AUD-R11 batch-06): NpgsqlBinaryImporter
/// driven entirely via reflection to avoid a hard Npgsql dependency in the provider itself.
/// Previously uncovered: no test in tests/ referenced this provider at all, and it's
/// unreachable via BulkCopyIntegrationTests.cs because that suite never calls
/// UseNativeBulkCopy() (see BulkCopyDialectFactoryTests.cs for that gap).
/// Skipped dynamically when no local PostgreSQL server is reachable.
/// </summary>
public class PostgreSqlBulkCopyProviderTests
{
    private static NpgsqlConnection OpenOrSkip()
    {
        if (!TestConfiguration.HasPostgreSql)
        {
            Assert.Skip("PostgreSQL not configured. Set JAUNTY_TEST_POSTGRESQL or ConnectionStrings:PostgreSql.");
        }

        var conn = new NpgsqlConnection(TestConfiguration.PostgreSqlConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            Assert.Skip($"PostgreSQL not reachable: {ex.Message}");
        }
        return conn;
    }

    private static DataTable MakeTable(int rows)
    {
        var table = new DataTable();
        table.Columns.Add("name", typeof(string));
        table.Columns.Add("price", typeof(decimal));
        table.Columns.Add("stock", typeof(int));
        table.Columns.Add("discontinued", typeof(bool));
        for (int i = 0; i < rows; i++)
        {
            var row = table.NewRow();
            row["name"] = i % 97 == 0 ? (object)DBNull.Value : $"prod-{i}";
            row["price"] = 0.5m + i;
            row["stock"] = i % 250;
            row["discontinued"] = i % 3 == 0;
            table.Rows.Add(row);
        }
        return table;
    }

    private static void CreateTable(NpgsqlConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DROP TABLE IF EXISTS "{name}";
            CREATE TABLE "{name}" (
                id BIGSERIAL PRIMARY KEY,
                name TEXT NULL,
                price NUMERIC(18,2) NOT NULL,
                stock INTEGER NOT NULL,
                discontinued BOOLEAN NOT NULL)
            """;
        cmd.ExecuteNonQuery();
    }

    private static long Count(NpgsqlConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public void IsSupported_NpgsqlReferenced_IsTrue()
    {
        Assert.True(new PostgreSqlBulkCopyProvider().IsSupported);
    }

    [Fact]
    public void CopyToServer_MultipleChunks_InsertsAllRowsAndValues()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_pg_sync");

        using var reader = MakeTable(1200).CreateDataReader();
        int inserted = new PostgreSqlBulkCopyProvider().CopyToServer(
            conn, "bulk_pg_sync", reader, new BulkCopyOptions());

        Assert.Equal(1200, inserted);
        Assert.Equal(1200, Count(conn, "bulk_pg_sync"));

        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT name, price, stock, discontinued FROM \"bulk_pg_sync\" ORDER BY id OFFSET 1 LIMIT 1";
            using var r = check.ExecuteReader();
            Assert.True(r.Read());
            Assert.Equal("prod-1", r.GetString(0));
            Assert.Equal(1.5m, r.GetDecimal(1));
            Assert.Equal(1, r.GetInt32(2));
            Assert.False(r.GetBoolean(3));
        }

        // NULL round-trip (every 97th row has a DBNull name)
        using var nullCheck = conn.CreateCommand();
        nullCheck.CommandText = "SELECT COUNT(*) FROM \"bulk_pg_sync\" WHERE name IS NULL";
        Assert.Equal(1200 / 97 + 1, Convert.ToInt32(nullCheck.ExecuteScalar()));
    }

    [Fact]
    public async Task CopyToServerAsync_InsertsAllRows()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_pg_async");

        using var reader = MakeTable(750).CreateDataReader();
        int inserted = await new PostgreSqlBulkCopyProvider().CopyToServerAsync(
            conn, "bulk_pg_async", reader, new BulkCopyOptions(), CancellationToken.None);

        Assert.Equal(750, inserted);
        Assert.Equal(750, Count(conn, "bulk_pg_async"));
    }

    [Fact]
    public void CopyToServer_InvalidTableName_ThrowsArgumentException()
    {
        using var conn = OpenOrSkip();

        using var reader = MakeTable(1).CreateDataReader();
        Assert.Throws<ArgumentException>(() => new PostgreSqlBulkCopyProvider().CopyToServer(
            conn, "products\"; DROP TABLE users; --", reader, new BulkCopyOptions()));
    }

    [Fact]
    public void CopyToServer_NonNpgsqlConnection_ThrowsArgumentException()
    {
        using var conn = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        conn.Open();
        using var reader = MakeTable(1).CreateDataReader();

        var ex = Assert.Throws<ArgumentException>(() =>
            new PostgreSqlBulkCopyProvider().CopyToServer(conn, "irrelevant", reader, new BulkCopyOptions()));

        Assert.Contains("NpgsqlConnection", ex.Message);
    }

    [Fact]
    public async Task CopyToServerAsync_NonNpgsqlConnection_ThrowsArgumentException()
    {
        using var conn = new System.Data.SQLite.SQLiteConnection("Data Source=:memory:");
        conn.Open();
        using var reader = MakeTable(1).CreateDataReader();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            new PostgreSqlBulkCopyProvider().CopyToServerAsync(conn, "irrelevant", reader, new BulkCopyOptions(), CancellationToken.None).AsTask());

        Assert.Contains("NpgsqlConnection", ex.Message);
    }
}
#endif
