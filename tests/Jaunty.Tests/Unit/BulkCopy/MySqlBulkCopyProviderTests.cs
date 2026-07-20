#if NET8_0_OR_GREATER
using System.Data;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection.BulkCopy;

using MySql.Data.MySqlClient;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// Integration tests for the rewritten MySqlBulkCopyProvider (PROD-120):
/// chunked multi-row INSERT, no LOAD DATA LOCAL INFILE / local_infile dependency.
/// Skipped dynamically when no local MariaDB/MySQL server is reachable.
/// </summary>
public class MySqlBulkCopyProviderTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_MARIADB")
        ?? "Server=localhost;Database=jauntybench;User=root;";

    private static MySqlConnection OpenOrSkip()
    {
        var conn = new MySqlConnection(ConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            Assert.Skip($"MariaDB/MySQL not reachable: {ex.Message}");
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

    private static void CreateTable(MySqlConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            DROP TABLE IF EXISTS `{name}`;
            CREATE TABLE `{name}` (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(64) NULL,
                price DECIMAL(18,2) NOT NULL,
                stock INT NOT NULL,
                discontinued TINYINT(1) NOT NULL)
            """;
        cmd.ExecuteNonQuery();
    }

    private static long Count(MySqlConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM `{table}`";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public void CopyToServer_MultipleChunks_InsertsAllRowsAndValues()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mysql_sync");

        // 4 columns -> 500 rows per chunk; 1200 rows = 2 full chunks + partial tail
        using var reader = MakeTable(1200).CreateDataReader();
        // project away nothing: DataTableReader exposes exactly the 4 data columns
        int inserted = new MySqlBulkCopyProvider().CopyToServer(
            conn, "bulk_mysql_sync", reader, new BulkCopyOptions());

        Assert.Equal(1200, inserted);
        Assert.Equal(1200, Count(conn, "bulk_mysql_sync"));

        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT name, price, stock, discontinued FROM `bulk_mysql_sync` ORDER BY id LIMIT 1 OFFSET 1";
            using var r = check.ExecuteReader();
            Assert.True(r.Read());
            Assert.Equal("prod-1", r.GetString(0));
            Assert.Equal(1.5m, r.GetDecimal(1));
            Assert.Equal(1, r.GetInt32(2));
            Assert.False(r.GetBoolean(3));
        }

        // NULL round-trip (every 97th row has a DBNull name)
        using var nullCheck = conn.CreateCommand();
        nullCheck.CommandText = "SELECT COUNT(*) FROM `bulk_mysql_sync` WHERE name IS NULL";
        Assert.Equal(1200 / 97 + 1, Convert.ToInt32(nullCheck.ExecuteScalar()));
    }

    [Fact]
    public async Task CopyToServerAsync_InsertsAllRows()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mysql_async");

        using var reader = MakeTable(750).CreateDataReader();
        int inserted = await new MySqlBulkCopyProvider().CopyToServerAsync(
            conn, "bulk_mysql_async", reader, new BulkCopyOptions(), CancellationToken.None);

        Assert.Equal(750, inserted);
        Assert.Equal(750, Count(conn, "bulk_mysql_async"));
    }

    [Fact]
    public void CopyToServer_RespectsExternalTransaction_RollbackDiscardsRows()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mysql_txn");

        using (var txn = conn.BeginTransaction())
        {
            using var reader = MakeTable(50).CreateDataReader();
            int inserted = new MySqlBulkCopyProvider().CopyToServer(
                conn, "bulk_mysql_txn", reader, new BulkCopyOptions { Transaction = txn });
            Assert.Equal(50, inserted);
            txn.Rollback();
        }

        Assert.Equal(0, Count(conn, "bulk_mysql_txn"));
    }

    [Fact]
    public void CopyToServer_InvalidTableName_ThrowsArgumentException()
    {
        // Regression test (AUD-R11): BuildChunkCommand used to interpolate tableName/columnName
        // via EscapeIdentifier (backtick-doubling) without validating them first, unlike
        // PostgreSqlBulkCopyProvider.BuildCopyCommand, which validates via SqlIdentifierValidator
        // before escaping. A backtick can't break out of MySQL's own backtick-doubling escape,
        // but this still closes the gap so both providers enforce the same identifier contract.
        using var conn = OpenOrSkip();

        using var reader = MakeTable(1).CreateDataReader();
        Assert.Throws<ArgumentException>(() => new MySqlBulkCopyProvider().CopyToServer(
            conn, "products; DROP TABLE users; --", reader, new BulkCopyOptions()));
    }
}
#endif
