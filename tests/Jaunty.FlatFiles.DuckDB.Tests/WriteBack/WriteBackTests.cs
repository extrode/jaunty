using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

/// <summary>
/// Tests for SaveAsync and ExportAsync write-back operations.
/// Covers T045 (non-destructive save), T046 (destructive save), T047 (cross-format export),
/// T048 (format inference), T054–T056.
/// </summary>
public class WriteBackTests : IDisposable
{
    private readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_writeback_tests_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    public WriteBackTests()
    {
        Directory.CreateDirectory(DataDir);
        _csvPath = Path.Combine(DataDir, "inventory.csv");

        // Generate a CSV file using DuckDB
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
        try { Directory.Delete(DataDir, true); } catch { }
    }

    // ==========================================
    // T045 — SaveAsync non-destructive
    // ==========================================

    [Fact]
    public async Task SaveAsync_NonDestructive_WritesToNewFile()
    {
        var outputPath = Path.Combine(DataDir, "output.csv");

        // Mutate data
        await _db.InsertAsync(new InventoryItem
        {
            ItemId = 10,
            ItemName = "New Item",
            Category = "Test",
            StockQuantity = 1,
            UnitPrice = 1.00m,
            InStock = true
        });

        // Save to new file
        await _db.SaveAsync<InventoryItem>(outputPath);

        Assert.True(File.Exists(outputPath));

        // Verify the new file has 6 rows by loading it in a fresh DuckDB
        using var verifyConn = new DuckDBConnection("DataSource=:memory:");
        verifyConn.Open();
        using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_csv('{outputPath.Replace("\\", "/").Replace("'", "''")}')";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task SaveAsync_NonDestructive_OriginalUnchanged()
    {
        // Read original file bytes before mutation
        var originalBytes = File.ReadAllBytes(_csvPath);

        // Mutate data
        await _db.InsertAsync(new InventoryItem
        {
            ItemId = 10,
            ItemName = "New Item",
            Category = "Test",
            StockQuantity = 1,
            UnitPrice = 1.00m,
            InStock = true
        });

        // Save to a DIFFERENT file
        var outputPath = Path.Combine(DataDir, "output.csv");
        await _db.SaveAsync<InventoryItem>(outputPath);

        // Original file should be byte-identical
        var afterBytes = File.ReadAllBytes(_csvPath);
        Assert.Equal(originalBytes, afterBytes);
    }

    [Fact]
    public async Task SaveAsync_NonDestructive_WithoutMutation_ExportsAllData()
    {
        var outputPath = Path.Combine(DataDir, "output_no_mutation.csv");

        // Save without any mutations
        await _db.SaveAsync<InventoryItem>(outputPath);

        Assert.True(File.Exists(outputPath));

        // Should have 5 rows
        using var verifyConn = new DuckDBConnection("DataSource=:memory:");
        verifyConn.Open();
        using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_csv('{outputPath.Replace("\\", "/").Replace("'", "''")}')";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(5, count);
    }

    // ==========================================
    // T046 — SaveAsync destructive (overwrite)
    // ==========================================

    [Fact]
    public async Task SaveAsync_Overwrite_ReplacesOriginalFile()
    {
        // Mutate data
        await _db.InsertAsync(new InventoryItem
        {
            ItemId = 10,
            ItemName = "New Item",
            Category = "Test",
            StockQuantity = 1,
            UnitPrice = 1.00m,
            InStock = true
        });

        // Save with overwrite
        await _db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite);

        // Original file should be replaced — verify it has 6 rows
        using var verifyConn = new DuckDBConnection("DataSource=:memory:");
        verifyConn.Open();
        using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_csv('{_csvPath.Replace("\\", "/").Replace("'", "''")}')";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task SaveAsync_Overwrite_AfterDelete_ReflectsChanges()
    {
        // Delete two rows
        await _db.DeleteAsync<InventoryItem>(i => i.Category == "Hardware");

        // Save with overwrite
        await _db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite);

        // Verify the file now has 3 rows
        using var verifyConn = new DuckDBConnection("DataSource=:memory:");
        verifyConn.Open();
        using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_csv('{_csvPath.Replace("\\", "/").Replace("'", "''")}')";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task SaveAsync_NewFileMode_WithoutPath_ThrowsDescriptiveError()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _db.SaveAsync<InventoryItem>(WriteBackMode.NewFile);
        });
    }

    // ==========================================
    // T047 — ExportAsync cross-format
    // ==========================================

    [Fact]
    public async Task ExportAsync_CsvToParquet_WritesValidFile()
    {
        var parquetPath = Path.Combine(DataDir, "export.parquet");

        await _db.ExportAsync<InventoryItem>(parquetPath);

        Assert.True(File.Exists(parquetPath));

        // Verify the Parquet file has 5 rows
        using var verifyConn = new DuckDBConnection("DataSource=:memory:");
        verifyConn.Open();
        using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_parquet('{parquetPath.Replace("\\", "/").Replace("'", "''")}')";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task ExportAsync_CsvToJson_WritesValidFile()
    {
        var jsonPath = Path.Combine(DataDir, "export.json");

        await _db.ExportAsync<InventoryItem>(jsonPath);

        Assert.True(File.Exists(jsonPath));

        // Verify the JSON file has data
        using var verifyConn = new DuckDBConnection("DataSource=:memory:");
        verifyConn.Open();
        using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_json_auto('{jsonPath.Replace("\\", "/").Replace("'", "''")}')";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task ExportAsync_AfterMutation_IncludesChanges()
    {
        var parquetPath = Path.Combine(DataDir, "mutated_export.parquet");

        // Insert and then export
        await _db.InsertAsync(new InventoryItem
        {
            ItemId = 10,
            ItemName = "Extra",
            Category = "Test",
            StockQuantity = 1,
            UnitPrice = 1.00m,
            InStock = true
        });

        await _db.ExportAsync<InventoryItem>(parquetPath);

        // Verify it has 6 rows
        using var verifyConn = new DuckDBConnection("DataSource=:memory:");
        verifyConn.Open();
        using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_parquet('{parquetPath.Replace("\\", "/").Replace("'", "''")}')";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(6, count);
    }

    [Fact]
    public async Task ExportAsync_CsvToTsv_WritesValidFile()
    {
        var tsvPath = Path.Combine(DataDir, "export.tsv");

        await _db.ExportAsync<InventoryItem>(tsvPath);

        Assert.True(File.Exists(tsvPath));

        // Verify it's tab-delimited
        var content = File.ReadAllText(tsvPath);
        Assert.Contains("\t", content);
    }

    // ==========================================
    // T048 — Output format inference from extension
    // ==========================================

    [Fact]
    public async Task FormatInference_Csv_InfersCorrectly()
    {
        var path = Path.Combine(DataDir, "inferred.csv");
        await _db.ExportAsync<InventoryItem>(path);
        Assert.True(File.Exists(path));
        // Verify it's comma-delimited (not tab, not binary)
        var content = File.ReadAllText(path);
        Assert.Contains(",", content);
        Assert.DoesNotContain("\t", content);
    }

    [Fact]
    public async Task FormatInference_Parquet_InfersCorrectly()
    {
        var path = Path.Combine(DataDir, "inferred.parquet");
        await _db.ExportAsync<InventoryItem>(path);
        Assert.True(File.Exists(path));
        // Parquet files start with PAR1 magic bytes
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 4);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'A', bytes[1]);
        Assert.Equal((byte)'R', bytes[2]);
        Assert.Equal((byte)'1', bytes[3]);
    }

    [Fact]
    public async Task FormatInference_UnsupportedExtension_Throws()
    {
        var path = Path.Combine(DataDir, "bad.xyz");
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _db.ExportAsync<InventoryItem>(path);
        });
    }

    [Fact]
    public async Task FormatInference_LegacyXls_Throws()
    {
        // AUD-R14 batch-7: ".xls" used to map to FileFormats.Excel here, but DuckDB's write path
        // can only produce modern ".xlsx" content - writing that under a ".xls" name produced a
        // file FlatFile.cs's read-side _extensionRegistry (which only registers ".xlsx") can never
        // read back, and isn't a genuine legacy .xls file despite the extension. Now rejected like
        // any other unsupported extension.
        var path = Path.Combine(DataDir, "legacy.xls");
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _db.ExportAsync<InventoryItem>(path);
        });
    }

    // ==========================================
    // M3 Exit Gate — Full INSERT/UPDATE/DELETE cycle with write-back
    // ==========================================

    [Fact]
    public async Task ExitGate_FullCrudCycle_WithWriteBack()
    {
        // 1. Read original data
        var originalCount = _db.Connection.From<InventoryItem>().Count();
        Assert.Equal(5, originalCount);

        // 2. INSERT
        await _db.InsertAsync(new InventoryItem
        {
            ItemId = 20,
            ItemName = "Exit Gate Item",
            Category = "ExitGate",
            StockQuantity = 100,
            UnitPrice = 50.00m,
            InStock = true
        });
        Assert.Equal(6, _db.Connection.From<InventoryItem>().Count());

        // 3. UPDATE
        await _db.UpdateAsync<InventoryItem>(
            i => i.ItemId == 20,
            i => i.StockQuantity,
            200);
        var updated = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 20).Select();
        Assert.Equal(200, updated[0].StockQuantity);

        // 4. DELETE
        await _db.DeleteAsync<InventoryItem>(i => i.Category == "Misc");
        Assert.Equal(5, _db.Connection.From<InventoryItem>().Count());

        // 5. IsModified
        Assert.True(_db.IsModified<InventoryItem>());

        // 6. Non-destructive save to CSV
        var csvOutput = Path.Combine(DataDir, "exitgate_output.csv");
        await _db.SaveAsync<InventoryItem>(csvOutput);
        Assert.True(File.Exists(csvOutput));

        // 7. Non-destructive save to Parquet (cross-format export)
        var parquetOutput = Path.Combine(DataDir, "exitgate_output.parquet");
        await _db.ExportAsync<InventoryItem>(parquetOutput);
        Assert.True(File.Exists(parquetOutput));

        // 8. Destructive save (overwrite original CSV)
        await _db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite);

        // 9. Verify: reload the overwritten CSV and confirm it has the right data
        using var verifyConn = new DuckDBConnection("DataSource=:memory:");
        verifyConn.Open();
        using var cmd = verifyConn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM read_csv('{_csvPath.Replace("\\", "/").Replace("'", "''")}')";
        var finalCount = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(5, finalCount); // 5 original - 1 deleted + 1 inserted = 5
    }
}