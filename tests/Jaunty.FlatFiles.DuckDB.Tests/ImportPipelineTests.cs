using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Entities;

using Microsoft.Data.Sqlite;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// Tests for the M4 Import Pipeline.
/// Covers T059–T076: ImportIntoAsync, CreateTableIfMissing, ConflictStrategy, FlatFileImporter,
/// progress reporting, schema validation, and cross-format import.
/// </summary>
public class ImportPipelineTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_import_tests_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly string _parquetPath;
    private readonly DuckDb _db;

    public ImportPipelineTests()
    {
        Directory.CreateDirectory(DataDir);
        _csvPath = Path.Combine(DataDir, "inventory.csv");
        _parquetPath = Path.Combine(DataDir, "inventory.parquet");

        // Generate CSV and Parquet test files using DuckDB
        using var genConnection = new DuckDBConnection("DataSource=:memory:");
        genConnection.Open();
        using var cmd = genConnection.CreateCommand();

        // CSV
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

        // Parquet
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true),
                    (2, 'Widget B', 'Electronics', 0, 49.99, false),
                    (3, 'Gadget C', 'Hardware', 75, 15.50, true),
                    (4, 'Gadget D', 'Hardware', 200, 8.25, true),
                    (5, 'Thingamajig', 'Misc', 10, 99.99, true)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{_parquetPath.Replace("\\", "/").Replace("'", "''")}' (FORMAT PARQUET)";
        cmd.ExecuteNonQuery();

        var options = new FlatFileDatabaseOptions();
        options.AddCsv<InventoryItem>(_csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(DataDir, true); } catch { }
    }

    private static SqliteConnection CreateSqliteConnection()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        return conn;
    }

    private static void CreateInventoryTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE ""inventory"" (
                ""ItemId"" INTEGER PRIMARY KEY,
                ""ItemName"" TEXT NOT NULL,
                ""Category"" TEXT NOT NULL,
                ""StockQuantity"" INTEGER NOT NULL,
                ""UnitPrice"" REAL NOT NULL,
                ""InStock"" INTEGER NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    private static long CountRows(SqliteConnection conn, string tableName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\"";
        return (long)cmd.ExecuteScalar()!;
    }

    // ==========================================
    // T071 — CSV → SQLite import end-to-end
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_CsvToSqlite_AllRowsImported()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        var count = await _db.ImportIntoAsync<InventoryItem>(sqlite);

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(sqlite, "inventory"));
    }

    [Fact]
    public async Task ImportIntoAsync_CsvToSqlite_DataIsCorrect()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        await _db.ImportIntoAsync<InventoryItem>(sqlite);

        // Verify a specific row
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT \"ItemName\", \"UnitPrice\" FROM \"inventory\" WHERE \"ItemId\" = 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("Widget A", reader.GetString(0));
        Assert.Equal(29.99, reader.GetDouble(1), 2);
    }

    [Fact]
    public async Task ImportIntoAsync_CsvToSqlite_TypesCorrect()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        await _db.ImportIntoAsync<InventoryItem>(sqlite);

        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT \"ItemId\", \"StockQuantity\", \"InStock\" FROM \"inventory\" WHERE \"ItemId\" = 2";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32(0));     // int
        Assert.Equal(0, reader.GetInt32(1));     // int
        Assert.Equal(0, reader.GetInt64(2));     // bool → INTEGER (false = 0)
    }

    // ==========================================
    // T072 — Parquet → SQLite import
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_ParquetToSqlite_AllRowsImported()
    {
        // Create a separate FlatFileDatabase with Parquet source
        var parquetOpts = new FlatFileDatabaseOptions();
        parquetOpts.AddParquet<InventoryItem>(_parquetPath);
        using var parquetDb = new DuckDb(parquetOpts);

        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        var count = await parquetDb.ImportIntoAsync<InventoryItem>(sqlite);

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(sqlite, "inventory"));
    }

    // ==========================================
    // T065 / T074 — CreateTableIfMissing
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_CreateTableIfMissing_CreatesTable()
    {
        using var sqlite = CreateSqliteConnection();
        // Do NOT create table manually

        var count = await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(createTableIfMissing: true));

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(sqlite, "inventory"));
    }

    [Fact]
    public async Task ImportIntoAsync_CreateTableIfMissing_SchemaCorrect()
    {
        using var sqlite = CreateSqliteConnection();

        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(createTableIfMissing: true));

        // Verify table structure
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(\"inventory\")";
        using var reader = cmd.ExecuteReader();

        var columns = new List<(string Name, string Type)>();
        while (reader.Read())
        {
            columns.Add((reader.GetString(1), reader.GetString(2)));
        }

        Assert.Contains(columns, c => c.Name == "ItemId" && c.Type == "INTEGER");
        Assert.Contains(columns, c => c.Name == "ItemName" && c.Type == "TEXT");
        Assert.Contains(columns, c => c.Name == "Category" && c.Type == "TEXT");
        Assert.Contains(columns, c => c.Name == "StockQuantity" && c.Type == "INTEGER");
        Assert.Contains(columns, c => c.Name == "UnitPrice" && c.Type == "REAL");
        Assert.Contains(columns, c => c.Name == "InStock" && c.Type == "INTEGER");
    }

    [Fact]
    public async Task ImportIntoAsync_CreateTableIfMissing_ExistingTable_DoesNotFail()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        // Should not throw even though table already exists
        var count = await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(createTableIfMissing: true));

        Assert.Equal(5, count);
    }

    // ==========================================
    // T066 — ConflictStrategy.Error
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_ConflictError_ThrowsOnDuplicate()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        // First import succeeds
        await _db.ImportIntoAsync<InventoryItem>(sqlite);

        // Second import should fail on duplicate primary key
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(onConflict: ConflictStrategy.Error));
        });
    }

    // ==========================================
    // T067 — ConflictStrategy.Skip
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_ConflictSkip_IgnoresDuplicates()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        // First import
        await _db.ImportIntoAsync<InventoryItem>(sqlite);
        Assert.Equal(5, CountRows(sqlite, "inventory"));

        // Second import with Skip — should not add duplicates
        var count = await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(onConflict: ConflictStrategy.Skip));

        // Still 5 rows — duplicates skipped
        Assert.Equal(5, CountRows(sqlite, "inventory"));
    }

    [Fact]
    public async Task ImportIntoAsync_ConflictSkip_NewRowsStillInserted()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        // First import: 5 rows
        await _db.ImportIntoAsync<InventoryItem>(sqlite);

        // Add a new row to the DuckDB source
        await _db.InsertAsync(new InventoryItem
        {
            ItemId = 10,
            ItemName = "New Item",
            Category = "Test",
            StockQuantity = 1,
            UnitPrice = 1.00m,
            InStock = true
        });

        // Import again with Skip — should insert the new row only
        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(onConflict: ConflictStrategy.Skip));

        Assert.Equal(6, CountRows(sqlite, "inventory"));
    }

    // ==========================================
    // T068 — ConflictStrategy.Upsert
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_ConflictUpsert_UpdatesExistingRows()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        // First import
        await _db.ImportIntoAsync<InventoryItem>(sqlite);

        // Modify a row in DuckDB
        await _db.UpdateAsync<InventoryItem>(
            i => i.ItemId == 1,
            i => i.StockQuantity,
            999);

        // Import again with Upsert
        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(onConflict: ConflictStrategy.Upsert));

        // Should still have 5 rows, but row 1 should have updated StockQuantity
        Assert.Equal(5, CountRows(sqlite, "inventory"));

        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT \"StockQuantity\" FROM \"inventory\" WHERE \"ItemId\" = 1";
        var qty = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(999, qty);
    }

    // ==========================================
    // T062 — SQLite import optimizer (batched + transaction)
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_BatchSize_RespectsBatchSize()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        var progressCallCount = 0;
        var lastReported = 0L;

        var count = await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(batchSize: 2, onProgress: (imported, _) =>
        {
            progressCallCount++;
            lastReported = imported;
        }));

        Assert.Equal(5, count);
        // With batch size 2 and 5 rows: progress at 2, 4, then final at 5
        Assert.True(progressCallCount >= 3, $"Expected at least 3 progress calls, got {progressCallCount}");
    }

    // ==========================================
    // T069 / T075 — Progress reporting
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_ProgressCallback_Fires()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        var progressReports = new List<(long Imported, long? Total)>();

        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(batchSize: 3, onProgress: (imported, total) =>
        {
            progressReports.Add((imported, total));
        }));

        Assert.NotEmpty(progressReports);

        // Final callback should report total = totalImported
        var last = progressReports[^1];
        Assert.Equal(5, last.Imported);
        Assert.Equal(5L, last.Total);
    }

    [Fact]
    public async Task ImportIntoAsync_ProgressCallback_ReportsIncreasingCounts()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        var importedCounts = new List<long>();

        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(batchSize: 2, onProgress: (imported, _) =>
        {
            importedCounts.Add(imported);
        }));

        // Counts should be non-decreasing
        for (int i = 1; i < importedCounts.Count; i++)
        {
            Assert.True(importedCounts[i] >= importedCounts[i - 1],
                $"Progress counts should be non-decreasing: {importedCounts[i - 1]} -> {importedCounts[i]}");
        }
    }

    // ==========================================
    // T070 — Schema alignment validation
    // ==========================================

    [Fact]
    public async Task ImportIntoAsync_SchemaMismatch_ThrowsBeforeImport()
    {
        using var sqlite = CreateSqliteConnection();

        // Create a table with wrong columns
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE ""inventory"" (
                ""WrongColumn1"" TEXT,
                ""WrongColumn2"" INTEGER
            )";
        cmd.ExecuteNonQuery();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _db.ImportIntoAsync<InventoryItem>(sqlite);
        });

        // Verify no rows were inserted
        Assert.Equal(0, CountRows(sqlite, "inventory"));
    }

    // ==========================================
    // T061 — FlatFileImporter shorthand
    // ==========================================

    [Fact]
    public async Task FlatFileImporter_ImportAsync_CsvToSqlite()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        var count = await FlatFileImporter.ImportAsync<InventoryItem>(
            _csvPath, sqlite);

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(sqlite, "inventory"));
    }

    [Fact]
    public async Task FlatFileImporter_ImportAsync_WithOptions()
    {
        using var sqlite = CreateSqliteConnection();

        var count = await FlatFileImporter.ImportAsync<InventoryItem>(
            _csvPath, sqlite,
            new ImportOptions(createTableIfMissing: true, batchSize: 2));

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(sqlite, "inventory"));
    }

    [Fact]
    public async Task FlatFileImporter_ImportAsync_ParquetToSqlite()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        var count = await FlatFileImporter.ImportAsync<InventoryItem>(
            _parquetPath, sqlite);

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(sqlite, "inventory"));
    }

    [Fact]
    public async Task FlatFileImporter_ImportAsync_ConfiguredSource()
    {
        using var sqlite = CreateSqliteConnection();
        CreateInventoryTable(sqlite);

        var count = await FlatFileImporter.ImportAsync<InventoryItem>(
            opts => opts.AddCsv<InventoryItem>(_csvPath),
            sqlite);

        Assert.Equal(5, count);
        Assert.Equal(5, CountRows(sqlite, "inventory"));
    }

    // ==========================================
    // M4 Exit Gate — Full import pipeline
    // ==========================================

    [Fact]
    public async Task ExitGate_CsvImportToSqlite_AllConflictStrategies()
    {
        using var sqlite = CreateSqliteConnection();

        // 1. Import with CreateTableIfMissing
        var imported = await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(createTableIfMissing: true));
        Assert.Equal(5, imported);
        Assert.Equal(5, CountRows(sqlite, "inventory"));

        // 2. ConflictStrategy.Error — should throw on duplicate
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(onConflict: ConflictStrategy.Error));
        });

        // 3. ConflictStrategy.Skip — should not change count
        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(onConflict: ConflictStrategy.Skip));
        Assert.Equal(5, CountRows(sqlite, "inventory"));

        // 4. Mutate in DuckDB, then upsert
        await _db.UpdateAsync<InventoryItem>(
            i => i.ItemId == 1,
            i => i.ItemName,
            "Updated Widget A");

        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(onConflict: ConflictStrategy.Upsert));
        Assert.Equal(5, CountRows(sqlite, "inventory"));

        // Verify the upserted row
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT \"ItemName\" FROM \"inventory\" WHERE \"ItemId\" = 1";
        var name = (string)cmd.ExecuteScalar()!;
        Assert.Equal("Updated Widget A", name);

        // 5. Progress callback worked
        var progressFired = false;
        await _db.ImportIntoAsync<InventoryItem>(sqlite, new ImportOptions(onConflict: ConflictStrategy.Upsert, onProgress: (_, _) => progressFired = true));

        Assert.True(progressFired);
    }
}
