using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;

namespace Jaunty.FlatFiles.DuckDB.Tests.Write;

/// <summary>
/// AUD-R25 (B7-9): six of the eight sites that raise the per-entity modified flag guard on
/// rows-affected; the two single-entity <c>Insert</c> overloads set it unconditionally. A
/// single-row VALUES insert affects exactly one row or throws, so the two forms agree today - these
/// tests pin the flag's contract at all four write verbs so the guard cannot be dropped again
/// without something failing.
/// </summary>
public class ModifiedFlagConsistencyTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_r25_modified_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public ModifiedFlagConsistencyTests()
    {
        Directory.CreateDirectory(_dataDir);
        var csvPath = Path.Combine(_dataDir, "inventory.csv");

        using (var generator = new DuckDBConnection("DataSource=:memory:"))
        {
            generator.Open();
            using DuckDBCommand cmd = generator.CreateCommand();
            cmd.CommandText = $@"
                COPY (
                    SELECT * FROM (VALUES
                        (1, 'Widget A', 'Electronics', 150, 29.99, true),
                        (2, 'Widget B', 'Hardware', 0, 49.99, false)
                    ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
                ) TO '{csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
            cmd.ExecuteNonQuery();
        }

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    [Fact]
    public void IsModified_IsFalseBeforeAnyWrite()
    {
        Assert.False(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public void Insert_SingleEntity_MarksModified()
    {
        _db.Insert(new InventoryItem { ItemId = 3, ItemName = "Gadget", Category = "Misc", StockQuantity = 5, UnitPrice = 1.5m, InStock = true });

        Assert.True(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public async Task InsertAsync_SingleEntity_MarksModified()
    {
        await _db.InsertAsync(new InventoryItem { ItemId = 4, ItemName = "Gizmo", Category = "Misc", StockQuantity = 5, UnitPrice = 1.5m, InStock = true });

        Assert.True(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public void Insert_Batch_MarksModified()
    {
        _db.Insert(
        [
            new InventoryItem { ItemId = 5, ItemName = "Doohickey", Category = "Misc", StockQuantity = 1, UnitPrice = 2m, InStock = true },
            new InventoryItem { ItemId = 6, ItemName = "Thingamabob", Category = "Misc", StockQuantity = 2, UnitPrice = 3m, InStock = true },
        ]);

        Assert.True(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public void Insert_EmptyBatch_DoesNotMarkModified()
    {
        _db.Insert(Array.Empty<InventoryItem>());

        Assert.False(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public void Update_MatchingNothing_DoesNotMarkModified()
    {
        // The rows-affected guard is what makes this false rather than "we ran an UPDATE".
        _db.Update<InventoryItem>(i => i.StockQuantity == 99999, i => i.StockQuantity, 1);

        Assert.False(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public void Delete_MatchingNothing_DoesNotMarkModified()
    {
        _db.Delete<InventoryItem>(i => i.ItemId == 99999);

        Assert.False(_db.IsModified<InventoryItem>());
    }

    [Fact]
    public void Delete_MatchingSomething_MarksModified()
    {
        _db.Delete<InventoryItem>(i => i.ItemId == 1);

        Assert.True(_db.IsModified<InventoryItem>());
    }
}
