#if NET8_0_OR_GREATER
using System.Data;

using Jaunty.Configuration;
using Jaunty.Extensions.Reflection.BulkCopy;

using Microsoft.Data.SqlClient;

using Xunit;

namespace Jaunty.Tests.Unit.BulkCopy;

/// <summary>
/// Integration tests for SqlServerBulkCopyProvider (PROD-121): reflection-driven
/// SqlBulkCopy against a destination table with an IDENTITY column, which requires
/// explicit column mappings (default ordinal mapping would target the identity column).
/// Skipped dynamically when no local SQL Server is reachable.
/// </summary>
public class SqlServerBulkCopyProviderTests
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("JAUNTY_TEST_SQLSERVER")
        ?? "Server=localhost;Database=JauntyBench;Trusted_Connection=True;TrustServerCertificate=True;";

    private static SqlConnection OpenOrSkip()
    {
        var conn = new SqlConnection(ConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            Assert.Skip($"SQL Server not reachable: {ex.Message}");
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

    private static void CreateTable(SqlConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            IF OBJECT_ID('{name}', 'U') IS NOT NULL DROP TABLE [{name}];
            CREATE TABLE [{name}] (
                id INT IDENTITY(1,1) PRIMARY KEY,
                name NVARCHAR(64) NULL,
                price DECIMAL(18,2) NOT NULL,
                stock INT NOT NULL,
                discontinued BIT NOT NULL)
            """;
        cmd.ExecuteNonQuery();
    }

    private static long Count(SqlConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT_BIG(*) FROM [{table}]";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public void CopyToServer_IdentityDestination_InsertsAllRowsAndValues()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mssql_sync");

        using var reader = MakeTable(1200).CreateDataReader();
        int inserted = new SqlServerBulkCopyProvider().CopyToServer(
            conn, "bulk_mssql_sync", reader, new BulkCopyOptions());

        Assert.Equal(1200, inserted);
        Assert.Equal(1200, Count(conn, "bulk_mssql_sync"));

        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT name, price, stock, discontinued FROM [bulk_mssql_sync] ORDER BY id OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY";
            using var r = check.ExecuteReader();
            Assert.True(r.Read());
            Assert.Equal("prod-1", r.GetString(0));
            Assert.Equal(1.5m, r.GetDecimal(1));
            Assert.Equal(1, r.GetInt32(2));
            Assert.False(r.GetBoolean(3));
        }

        // NULL round-trip (every 97th row has a DBNull name)
        using var nullCheck = conn.CreateCommand();
        nullCheck.CommandText = "SELECT COUNT(*) FROM [bulk_mssql_sync] WHERE name IS NULL";
        Assert.Equal(1200 / 97 + 1, Convert.ToInt32(nullCheck.ExecuteScalar()));
    }

    [Fact]
    public async Task CopyToServerAsync_InsertsAllRows()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mssql_async");

        using var reader = MakeTable(750).CreateDataReader();
        int inserted = await new SqlServerBulkCopyProvider().CopyToServerAsync(
            conn, "bulk_mssql_async", reader, new BulkCopyOptions(), CancellationToken.None);

        Assert.Equal(750, inserted);
        Assert.Equal(750, Count(conn, "bulk_mssql_async"));
    }

    [Fact]
    public void CopyToServer_RespectsExternalTransaction_RollbackDiscardsRows()
    {
        using var conn = OpenOrSkip();
        CreateTable(conn, "bulk_mssql_txn");

        using (var txn = conn.BeginTransaction())
        {
            using var reader = MakeTable(50).CreateDataReader();
            int inserted = new SqlServerBulkCopyProvider().CopyToServer(
                conn, "bulk_mssql_txn", reader, new BulkCopyOptions { Transaction = txn });
            Assert.Equal(50, inserted);
            txn.Rollback();
        }

        Assert.Equal(0, Count(conn, "bulk_mssql_txn"));
    }
}
#endif
