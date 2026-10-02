using DuckDB.NET.Data;

using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers;
using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

using Npgsql;

using Xunit;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.Import;

/// <summary>
/// coverage-gaps-2026-09-20: <c>ImportExecutor.ImportUsingDbBatchAsync</c> had no test.
/// <c>ImportPipelineTests</c>' Sqlite target never takes that branch -
/// <see cref="Microsoft.Data.Sqlite.SqliteConnection"/>'s <c>CanCreateBatch</c> is
/// <see langword="false"/> (confirmed live, 2026-09-20) - so every existing
/// <c>ImportIntoAsync</c> test exercises only the <c>ImportUsingSingleCommandAsync</c> fallback.
/// Npgsql's <see cref="NpgsqlConnection"/> does support <see cref="System.Data.Common.DbBatch"/>,
/// so it is the target here.
/// </summary>
public sealed class ImportUsingDbBatchLiveTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_dbbatch_tests_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    public ImportUsingDbBatchLiveTests()
    {
        Directory.CreateDirectory(_dataDir);
        _csvPath = Path.Combine(_dataDir, "inventory.csv");

        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true),
                    (2, 'Widget B', 'Electronics', 0, 49.99, false),
                    (3, 'Gadget C', 'Hardware', 75, 15.50, true),
                    (4, 'Gadget D', 'Hardware', 200, 8.25, true),
                    (5, 'Thingamajig', 'Misc', 10, 99.99, true)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmd.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(_csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    private static NpgsqlConnection OpenOrSkip()
    {
        var conn = new NpgsqlConnection(PostgreSqlTestConfiguration.ConnectionString);
        try
        {
            conn.Open();
        }
        catch (Exception ex)
        {
            conn.Dispose();
            if (PostgreSqlTestConfiguration.IsRequired)
                throw new InvalidOperationException($"PostgreSQL required by {PostgreSqlTestConfiguration.RequirePostgreSqlVariable} but unreachable: {ex.Message}", ex);

            Assert.Skip($"PostgreSQL not reachable: {ex.Message}");
        }
        return conn;
    }

    // CREATE TEMP TABLE, not a real table: this suite shares the torture-postgres "northwind"
    // database with every other Postgres-targeting suite, and concurrent net8.0/net10.0 legs (or
    // two developers) would otherwise race on DROP/CREATE/COUNT against the same table name. A
    // session-scoped temp table is visible only on this connection, needs no cleanup, and pg_temp
    // leads the default search_path so ImportUsingDbBatchAsync's own schema probe and INSERTs
    // resolve to it without any change on the production side.
    private static void CreateInventoryTable(NpgsqlConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TEMP TABLE "inventory" (
                "ItemId" INTEGER PRIMARY KEY,
                "ItemName" TEXT NOT NULL,
                "Category" TEXT NOT NULL,
                "StockQuantity" INTEGER NOT NULL,
                "UnitPrice" NUMERIC NOT NULL,
                "InStock" BOOLEAN NOT NULL
            )
            """;
        cmd.ExecuteNonQuery();
    }

    private static long CountRows(NpgsqlConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\"";
        return (long)cmd.ExecuteScalar()!;
    }

    [Fact]
    public void NpgsqlConnection_CanCreateBatch_IsTrue()
    {
        // Guards the premise this whole test class rests on: if a future Npgsql upgrade ever
        // flips this to false, ImportUsingDbBatchAsync would silently stop being covered again
        // rather than this suite failing loudly about it.
        using NpgsqlConnection conn = OpenOrSkip();

        Assert.True(conn.CanCreateBatch);
    }

    [Fact]
    public async Task ImportIntoAsync_CsvToPostgreSql_TakesTheDbBatchPath_AllRowsImported()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateInventoryTable(conn);

        long count = await _db.ImportIntoAsync<InventoryItem>(conn);

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(conn, "inventory"));
    }

    [Fact]
    public async Task ImportIntoAsync_CsvToPostgreSql_DataIsCorrect()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateInventoryTable(conn);

        await _db.ImportIntoAsync<InventoryItem>(conn);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """SELECT "ItemName", "UnitPrice", "InStock" FROM "inventory" WHERE "ItemId" = 1""";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Widget A", reader.GetString(0));
        Assert.Equal(29.99m, reader.GetDecimal(1));
        Assert.True(reader.GetBoolean(2));
    }

    /// <summary>
    /// <see cref="NpgsqlConnection_CanCreateBatch_IsTrue"/> proves the <c>if (CanCreateBatch)</c>
    /// gate takes this branch, but nothing here directly distinguishes
    /// <c>ImportUsingDbBatchAsync</c> from <c>ImportUsingSingleCommandAsync</c> by its own
    /// behaviour - and only <c>ImportUsingDbBatchAsync</c> clears and reuses the same
    /// <see cref="System.Data.Common.DbBatch"/> mid-stream (a batchSize smaller than the row
    /// count is required to exercise that reuse; the 5-row/1000-default fixture above never
    /// does). With batchSize 2 against 5 rows, only the DbBatch path reports a progress callback
    /// for the tail flush (<c>ImportUsingSingleCommandAsync</c> has none - only the two full-batch
    /// callbacks, then the overall final one from the caller): DbBatch reports
    /// (2,null),(4,null),(5,null),(5,5); single-command would report (2,null),(4,null),(5,5).
    /// </summary>
    [Fact]
    public async Task ImportIntoAsync_CsvToPostgreSql_WithBatchSizeSmallerThanRowCount_FlushesAndReusesTheBatch()
    {
        using NpgsqlConnection conn = OpenOrSkip();
        CreateInventoryTable(conn);

        var progress = new List<(long imported, long? total)>();
        var options = new ImportOptions(batchSize: 2, onProgress: (imported, total) => progress.Add((imported, total)));

        long count = await _db.ImportIntoAsync<InventoryItem>(conn, options);

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(conn, "inventory"));
        Assert.Equal(
            new (long, long?)[] { (2, null), (4, null), (5, null), (5, 5) },
            progress);
    }
}
