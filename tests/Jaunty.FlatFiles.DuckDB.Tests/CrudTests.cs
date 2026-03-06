using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Entities;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests;

/// <summary>
/// Tests for CRUD operations: INSERT, UPDATE, DELETE with table promotion.
/// Covers T039 (table promotion), T040–T043 (CRUD), T044 (IsModified), T051–T053, T057.
/// </summary>
public class CrudTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(Path.GetTempPath(), $"jaunty_crud_tests_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    public CrudTests()
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
    // T039 — Table promotion (VIEW → TABLE)
    // ==========================================

    [Fact]
    public void BeforeInsert_SourceIsView()
    {
        var source = _db.GetSource<InventoryItem>();
        Assert.NotNull(source);
        Assert.False(source.IsPromotedToTable);
        Assert.False(source.IsPreloaded);
    }

    [Fact]
    public async Task Insert_PromotesViewToTable()
    {
        var newItem = new InventoryItem
        {
            ItemId = 100, ItemName = "New Item", Category = "Test",
            StockQuantity = 1, UnitPrice = 9.99m, InStock = true
        };

        await _db.InsertAsync(newItem);

        var source = _db.GetSource<InventoryItem>();
        Assert.NotNull(source);
        Assert.True(source.IsPromotedToTable);
    }

    [Fact]
    public async Task TablePromotion_TransparentToQueries()
    {
        // Query before mutation
        var beforeResults = _db.Connection.From<InventoryItem>().Select();
        Assert.Equal(5, beforeResults.Count);

        // Insert triggers promotion
        var newItem = new InventoryItem
        {
            ItemId = 100, ItemName = "New Item", Category = "Test",
            StockQuantity = 1, UnitPrice = 9.99m, InStock = true
        };
        await _db.InsertAsync(newItem);

        // Query after mutation — should still work and include new row
        var afterResults = _db.Connection.From<InventoryItem>().Select();
        Assert.Equal(6, afterResults.Count);
    }

    // ==========================================
    // T040 — InsertAsync single row
    // ==========================================

    [Fact]
    public async Task InsertAsync_SingleRow_InsertsCorrectly()
    {
        var newItem = new InventoryItem
        {
            ItemId = 10, ItemName = "Sprocket", Category = "Hardware",
            StockQuantity = 500, UnitPrice = 3.50m, InStock = true
        };

        var affected = await _db.InsertAsync(newItem);
        Assert.Equal(1, affected);

        // Verify the row is queryable
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 10)
            .Select();

        Assert.Single(results);
        Assert.Equal("Sprocket", results[0].ItemName);
        Assert.Equal("Hardware", results[0].Category);
        Assert.Equal(500, results[0].StockQuantity);
        Assert.Equal(3.50m, results[0].UnitPrice);
        Assert.True(results[0].InStock);
    }

    [Fact]
    public async Task InsertAsync_SingleRow_PreservesExistingData()
    {
        var newItem = new InventoryItem
        {
            ItemId = 10, ItemName = "New", Category = "Test",
            StockQuantity = 1, UnitPrice = 1.00m, InStock = true
        };
        await _db.InsertAsync(newItem);

        // Original 5 rows + 1 new = 6
        var count = _db.Connection.From<InventoryItem>().Count();
        Assert.Equal(6, count);

        // Original row still intact
        var original = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 1)
            .Select();
        Assert.Single(original);
        Assert.Equal("Widget A", original[0].ItemName);
    }

    // ==========================================
    // T041 — InsertAsync batch
    // ==========================================

    [Fact]
    public async Task InsertAsync_Batch_InsertsAllRows()
    {
        var items = Enumerable.Range(100, 100).Select(i => new InventoryItem
        {
            ItemId = i, ItemName = $"Item_{i}", Category = "Batch",
            StockQuantity = i * 10, UnitPrice = i * 0.5m, InStock = true
        }).ToList();

        var affected = await _db.InsertAsync<InventoryItem>(items);
        Assert.Equal(100, affected);

        // Verify all rows present
        var batchResults = _db.Connection.From<InventoryItem>()
            .Where(i => i.Category == "Batch")
            .Select();
        Assert.Equal(100, batchResults.Count);

        // Total count = 5 original + 100 new
        var totalCount = _db.Connection.From<InventoryItem>().Count();
        Assert.Equal(105, totalCount);
    }

    [Fact]
    public async Task InsertAsync_EmptyBatch_ReturnsZero()
    {
        var affected = await _db.InsertAsync<InventoryItem>(Array.Empty<InventoryItem>());
        Assert.Equal(0, affected);
    }

    // ==========================================
    // T042 — UpdateAsync
    // ==========================================

    [Fact]
    public async Task UpdateAsync_MatchingRows_UpdatesCorrectly()
    {
        // Update all Electronics items to have StockQuantity = 999
        var affected = await _db.UpdateAsync<InventoryItem>(
            i => i.Category == "Electronics",
            i => i.StockQuantity,
            999);

        Assert.Equal(2, affected); // Widget A and Widget B

        // Verify the update
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.Category == "Electronics")
            .Select();
        Assert.All(results, r => Assert.Equal(999, r.StockQuantity));
    }

    [Fact]
    public async Task UpdateAsync_NoMatchingRows_ReturnsZero()
    {
        var affected = await _db.UpdateAsync<InventoryItem>(
            i => i.Category == "NonExistent",
            i => i.StockQuantity,
            999);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task UpdateAsync_StringColumn_UpdatesCorrectly()
    {
        var affected = await _db.UpdateAsync<InventoryItem>(
            i => i.ItemId == 1,
            i => i.ItemName,
            "Renamed Widget");

        Assert.Equal(1, affected);

        var result = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 1)
            .Select();
        Assert.Single(result);
        Assert.Equal("Renamed Widget", result[0].ItemName);
    }

    [Fact]
    public async Task UpdateAsync_BoolColumn_UpdatesCorrectly()
    {
        // Widget B is out of stock (InStock = false)
        var affected = await _db.UpdateAsync<InventoryItem>(
            i => i.ItemId == 2,
            i => i.InStock,
            true);

        Assert.Equal(1, affected);

        var result = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 2)
            .Select();
        Assert.Single(result);
        Assert.True(result[0].InStock);
    }

    // ==========================================
    // T043 — DeleteAsync
    // ==========================================

    [Fact]
    public async Task DeleteAsync_MatchingRows_DeletesCorrectly()
    {
        var affected = await _db.DeleteAsync<InventoryItem>(
            i => i.Category == "Misc");

        Assert.Equal(1, affected); // Thingamajig

        // Verify deletion
        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.Category == "Misc")
            .Select();
        Assert.Empty(results);

        // Other rows still present
        var totalCount = _db.Connection.From<InventoryItem>().Count();
        Assert.Equal(4, totalCount);
    }

    [Fact]
    public async Task DeleteAsync_NoMatchingRows_ReturnsZero()
    {
        var affected = await _db.DeleteAsync<InventoryItem>(
            i => i.Category == "NonExistent");

        Assert.Equal(0, affected);

        // All rows still present
        var count = _db.Connection.From<InventoryItem>().Count();
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task DeleteAsync_MultipleRows_DeletesAll()
    {
        var affected = await _db.DeleteAsync<InventoryItem>(
            i => i.Category == "Hardware");

        Assert.Equal(2, affected); // Gadget C and Gadget D

        var count = _db.Connection.From<InventoryItem>().Count();
        Assert.Equal(3, count);
    }

    // ==========================================
    // T044 — IsModified tracking
    // ==========================================

    [Fact]
    public void IsModified_BeforeMutation_ReturnsFalse()
    {
        Assert.False(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public async Task IsModified_AfterInsert_ReturnsTrue()
    {
        await _db.InsertAsync(new InventoryItem
        {
            ItemId = 100, ItemName = "New", Category = "Test",
            StockQuantity = 1, UnitPrice = 1.00m, InStock = true
        });

        Assert.True(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public async Task IsModified_AfterUpdate_ReturnsTrue()
    {
        await _db.UpdateAsync<InventoryItem>(
            i => i.ItemId == 1,
            i => i.StockQuantity,
            999);

        Assert.True(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public async Task IsModified_AfterDelete_ReturnsTrue()
    {
        await _db.DeleteAsync<InventoryItem>(i => i.ItemId == 5);
        Assert.True(_db.IsModified<InventoryItem>());
    }

    // ==========================================
    // T051-T053 — Full round-trip tests
    // ==========================================

    [Fact]
    public async Task FullRoundTrip_InsertThenQuery()
    {
        var newItem = new InventoryItem
        {
            ItemId = 50, ItemName = "Round Trip Item", Category = "Test",
            StockQuantity = 42, UnitPrice = 12.34m, InStock = true
        };

        await _db.InsertAsync(newItem);

        var result = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 50)
            .Select();

        Assert.Single(result);
        Assert.Equal("Round Trip Item", result[0].ItemName);
        Assert.Equal(42, result[0].StockQuantity);
        Assert.Equal(12.34m, result[0].UnitPrice);
    }

    [Fact]
    public async Task FullRoundTrip_UpdateThenQuery()
    {
        await _db.UpdateAsync<InventoryItem>(
            i => i.ItemId == 1,
            i => i.UnitPrice,
            99.99m);

        var result = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 1)
            .Select();

        Assert.Single(result);
        Assert.Equal(99.99m, result[0].UnitPrice);
    }

    [Fact]
    public async Task FullRoundTrip_DeleteThenQuery()
    {
        await _db.DeleteAsync<InventoryItem>(i => i.ItemId == 5);

        var result = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 5)
            .Select();

        Assert.Empty(result);
    }

    [Fact]
    public async Task FullRoundTrip_InsertUpdateDelete_Pipeline()
    {
        // Insert
        var newItem = new InventoryItem
        {
            ItemId = 200, ItemName = "Pipeline Item", Category = "Pipeline",
            StockQuantity = 10, UnitPrice = 5.00m, InStock = true
        };
        await _db.InsertAsync(newItem);
        Assert.Equal(6, _db.Connection.From<InventoryItem>().Count());

        // Update
        await _db.UpdateAsync<InventoryItem>(
            i => i.ItemId == 200,
            i => i.StockQuantity,
            0);
        var updated = _db.Connection.From<InventoryItem>()
            .Where(i => i.ItemId == 200)
            .Select();
        Assert.Single(updated);
        Assert.Equal(0, updated[0].StockQuantity);

        // Delete
        await _db.DeleteAsync<InventoryItem>(i => i.ItemId == 200);
        Assert.Equal(5, _db.Connection.From<InventoryItem>().Count());
    }

    // ==========================================
    // Predicate edge cases
    // ==========================================

    [Fact]
    public async Task Delete_CompoundPredicate_Works()
    {
        var affected = await _db.DeleteAsync<InventoryItem>(
            i => i.Category == "Electronics" && i.InStock == false);

        Assert.Equal(1, affected); // Widget B

        var count = _db.Connection.From<InventoryItem>().Count();
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task Update_GreaterThanPredicate_Works()
    {
        var affected = await _db.UpdateAsync<InventoryItem>(
            i => i.UnitPrice > 50m,
            i => i.Category,
            "Premium");

        Assert.Equal(1, affected); // Thingamajig (99.99)

        var results = _db.Connection.From<InventoryItem>()
            .Where(i => i.Category == "Premium")
            .Select();
        Assert.Single(results);
        Assert.Equal("Thingamajig", results[0].ItemName);
    }
}
