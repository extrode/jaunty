using DuckDB.NET.Data;

using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Extrode.Jaunty.FlatFiles.WriteBack;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

/// <summary>
/// The exact argument errors of <c>Save</c>/<c>SaveAsync</c>: an undefined <c>WriteBackMode</c>, a
/// null output path, and an output extension with no known format.
/// </summary>
public class WriteBackArgumentMessageTests : IDisposable
{
    private const string UndefinedModeMessage =
        "'99' is not a defined WriteBackMode. In-place write-back overwrites the source file " +
        "irreversibly, so only WriteBackMode.Overwrite is accepted here.";

    private const string UnknownExtensionPrefix =
        "Supported extensions: .csv, .tsv, .parquet, .json, .ndjson, .xlsx.";

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_wb_args_{Guid.NewGuid():N}");
    private readonly DuckDb _db;

    public WriteBackArgumentMessageTests()
    {
        Directory.CreateDirectory(_dataDir);
        string csvPath = Path.Combine(_dataDir, "inventory.csv");

        using var gen = new DuckDBConnection("DataSource=:memory:");
        gen.Open();
        using DuckDBCommand cmd = gen.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES (1, 'Widget A', 'Electronics', 150, 29.99, true))
                AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{csvPath.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ',')";
        cmd.ExecuteNonQuery();

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(csvPath);
        _db = new DuckDb(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_dataDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Save_AnUndefinedMode_IsRejectedWithItsReason()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _db.Save<InventoryItem>((WriteBackMode)99));

        Assert.StartsWith(UndefinedModeMessage, ex.Message);
    }

    [Fact]
    public async Task SaveAsync_AnUndefinedMode_IsRejectedWithItsReason()
    {
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await _db.SaveAsync<InventoryItem>((WriteBackMode)99));

        Assert.StartsWith(UndefinedModeMessage, ex.Message);
    }

    [Fact]
    public async Task SaveAsync_ANullOutputPath_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _db.SaveAsync<InventoryItem>((string)null!));

        Assert.Equal("outputPath", ex.ParamName);
    }

    [Fact]
    public async Task SaveAsync_AnXlsPath_PointsAtXlsx()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _db.SaveAsync<InventoryItem>(Path.Combine(_dataDir, "out.xls")));

        Assert.Equal(
            "Cannot infer output format from extension '.xls'. " + UnknownExtensionPrefix +
            " \".xls\" (legacy binary Excel) is not supported for writing - use \".xlsx\" instead. (Parameter 'path')",
            ex.Message);
    }

    [Fact]
    public async Task SaveAsync_AnUnknownExtension_ListsTheSupportedOnes()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _db.SaveAsync<InventoryItem>(Path.Combine(_dataDir, "out.foo")));

        Assert.Equal(
            "Cannot infer output format from extension '.foo'. " + UnknownExtensionPrefix + " (Parameter 'path')",
            ex.Message);
    }
}
