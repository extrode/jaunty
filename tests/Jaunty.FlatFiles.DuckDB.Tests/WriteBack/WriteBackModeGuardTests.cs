using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.FlatFiles.WriteBack;

namespace Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

/// <summary>
/// AUD-R26-063 (round 26, batch 7, low/bug). Two independent defects in the in-place write-back path,
/// both of which only show up when something unusual happens - which for an irreversible operation is
/// exactly when they matter.
///
/// <para>
/// <b>The destructive branch was the fallthrough.</b> <c>Save&lt;T&gt;(WriteBackMode)</c> and its
/// async twin validated the mode by rejecting exactly one value - <c>NewFile</c> - and overwrote the
/// caller's original file in place for everything else. <c>WriteBackMode</c> has two members today,
/// so a correct caller was unaffected; but C# permits an undefined enum value without a cast
/// diagnostic - <c>(WriteBackMode)99</c>, or a value deserialized from configuration - and that fell
/// straight through to the branch that destroys the source. The destructive path must be the one you
/// ask for by name.
/// </para>
///
/// <para>
/// <b>The temp file name was deterministic.</b> <c>.{name}.tmp{ext}</c> in the source's own
/// directory, derived from nothing but the original name. Two in-place saves of the same source
/// running concurrently therefore targeted the same path: the second <c>COPY TO</c> overwrote the
/// first's output before either <c>File.Move</c> ran, and one save silently persisted the other's
/// data. A leftover temp file from a hard exit - cleanup only runs in the <c>catch</c>, so a killed
/// process leaves one behind - was also indistinguishable from the current run's.
/// </para>
/// </summary>
public class WriteBackModeGuardTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_wb_guard_{Guid.NewGuid():N}");
    private readonly string _csvPath;
    private readonly DuckDb _db;

    public WriteBackModeGuardTests()
    {
        Directory.CreateDirectory(_dataDir);
        _csvPath = Path.Combine(_dataDir, "inventory.csv");

        using var gen = new DuckDBConnection("DataSource=:memory:");
        gen.Open();
        using DuckDBCommand cmd = gen.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true),
                    (2, 'Widget B', 'Electronics', 0, 49.99, false)
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

    // ------------------------------------------------------------------
    // The fallthrough
    // ------------------------------------------------------------------

    [Fact]
    public void Save_WithAnUndefinedMode_IsRejectedAndLeavesTheSourceUntouched()
    {
        string before = File.ReadAllText(_csvPath);

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => _db.Save<InventoryItem>((WriteBackMode)99));

        Assert.Equal("mode", ex.ParamName);
        // Not just the parameter name: the message must carry the offending value. The first draft
        // wrote it as $"'{{mode}}'", which in C# is a literal "{mode}" - and a test that asserted
        // only ParamName passed anyway. That is the "assertion that holds regardless of the fix"
        // shape, caught in review rather than by the suite.
        Assert.Contains("99", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{mode}", ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(_csvPath));
    }

    [Fact]
    public async Task SaveAsync_WithAnUndefinedMode_IsRejectedAndLeavesTheSourceUntouched()
    {
        string before = File.ReadAllText(_csvPath);

        ArgumentOutOfRangeException ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await _db.SaveAsync<InventoryItem>((WriteBackMode)99));

        Assert.Equal("mode", ex.ParamName);
        Assert.Contains("99", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("{mode}", ex.Message, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(_csvPath));
    }

    /// <summary>
    /// <c>NewFile</c> must keep its own message - it is a caller using the wrong overload, not a
    /// caller passing nonsense, and the two deserve different guidance.
    /// </summary>
    [Fact]
    public void Save_WithNewFile_StillNamesTheOtherOverload()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => _db.Save<InventoryItem>(WriteBackMode.NewFile));

        Assert.Contains("Save<T>(string outputPath)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_WithNewFile_StillNamesTheOtherOverload()
    {
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _db.SaveAsync<InventoryItem>(WriteBackMode.NewFile));

        Assert.Contains("SaveAsync<T>(string outputPath)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the one mode that is supposed to work still does - the guard rejects everything that is
    /// not <c>Overwrite</c>, so an over-tight guard would show up here.
    /// </summary>
    [Fact]
    public void Save_WithOverwrite_StillWritesInPlace()
    {
        _db.Update<InventoryItem>(i => i.ItemId == 1, i => i.ItemName!, "Renamed");

        _db.Save<InventoryItem>(WriteBackMode.Overwrite);

        Assert.Contains("Renamed", File.ReadAllText(_csvPath), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // The temp file name
    // ------------------------------------------------------------------

    /// <summary>
    /// The successful path leaves nothing behind, which is what makes the collision invisible in
    /// ordinary use and is worth pinning either way.
    /// </summary>
    [Fact]
    public void Save_WithOverwrite_LeavesNoTempFileBehind()
    {
        _db.Save<InventoryItem>(WriteBackMode.Overwrite);

        string[] leftovers = Directory.GetFiles(_dataDir, ".*", SearchOption.TopDirectoryOnly);
        Assert.Empty(leftovers);
    }

    // The unique-temp-name half of AUD-R26-063 has no direct test here, deliberately. Asserting it
    // would mean either reimplementing the path format in the test - which passes whatever the
    // implementation does and so verifies nothing - or racing two concurrent saves, which is
    // non-deterministic. The observable consequence that *can* be pinned is the one above: a
    // successful save leaves no temp file behind. The change itself is a one-line format change
    // reviewed in place; see DuckDbWriteBack.cs.
}
