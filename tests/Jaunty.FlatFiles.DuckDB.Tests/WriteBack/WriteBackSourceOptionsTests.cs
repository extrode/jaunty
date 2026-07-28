using DuckDB.NET.Data;

using Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Jaunty.FlatFiles.FileSources;
using Jaunty.FlatFiles.WriteBack;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

/// <summary>
/// AUD-R25: the write-back path discarded the source's own COPY TO configuration in two
/// independent ways.
///
/// <para>
/// <c>GenerateCopyToOptions()</c> hardcoded <c>HEADER true</c> on both CSV and TSV sources,
/// ignoring <c>HasHeader</c> - so an in-place <c>Save&lt;T&gt;(WriteBackMode.Overwrite)</c> on a
/// headerless file injected a header row that the source's own configuration could never read back;
/// the next read consumed the first data row as column names. AUD-R23 had already fixed the exact
/// same omission for <c>NullString</c> on these two methods.
/// </para>
///
/// <para>
/// Separately, <c>Save&lt;T&gt;(string)</c>/<c>Export&lt;T&gt;(string)</c> (and the async pair)
/// routed through the format-string <c>GenerateCopyToSql</c> overload, which knows only the format
/// name and emits a bare <c>FORMAT CSV, HEADER true</c>. Every source-configured option -
/// DELIMITER, QUOTE, NULL - was dropped, so a semicolon-delimited source silently wrote out
/// comma-delimited. Only <c>Save&lt;T&gt;(WriteBackMode)</c> used the source-aware overload.
/// </para>
/// </summary>
public class WriteBackSourceOptionsTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_r25_writeback_{Guid.NewGuid():N}");

    public WriteBackSourceOptionsTests() => Directory.CreateDirectory(_dataDir);

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    // ------------------------------------------------------------------
    // GenerateCopyToOptions honours HasHeader
    // ------------------------------------------------------------------

    [Fact]
    public void CsvSource_HasHeaderFalse_EmitsHeaderFalse()
    {
        var source = new CsvFileSource("t", "x.csv", typeof(InventoryItem)) { HasHeader = false };

        Assert.Contains("HEADER false", source.GenerateCopyToOptions());
    }

    [Fact]
    public void CsvSource_HasHeaderTrue_EmitsHeaderTrue()
    {
        var source = new CsvFileSource("t", "x.csv", typeof(InventoryItem)) { HasHeader = true };

        Assert.Contains("HEADER true", source.GenerateCopyToOptions());
    }

    [Fact]
    public void CsvSource_HasHeaderUnset_KeepsTheHeader()
    {
        // null means "auto-detect" on the read side. There is nothing to detect when writing, and a
        // file whose shape we were never told about is safer written with a header than without.
        var source = new CsvFileSource("t", "x.csv", typeof(InventoryItem));

        Assert.Contains("HEADER true", source.GenerateCopyToOptions());
    }

    [Fact]
    public void TsvSource_HasHeaderFalse_EmitsHeaderFalse_AndKeepsTheTabDelimiter()
    {
        var source = new TsvFileSource("t", "x.tsv", typeof(InventoryItem)) { HasHeader = false };

        string? options = source.GenerateCopyToOptions();
        Assert.Contains("HEADER false", options);
        Assert.Contains("DELIMITER '\t'", options);
    }

    [Fact]
    public void TsvSource_HasHeaderUnset_KeepsTheHeader()
    {
        var source = new TsvFileSource("t", "x.tsv", typeof(InventoryItem));

        Assert.Contains("HEADER true", source.GenerateCopyToOptions());
    }

    // ------------------------------------------------------------------
    // The destructive case: headerless in-place overwrite must round-trip
    // ------------------------------------------------------------------

    [Fact]
    public void Save_Overwrite_OnHeaderlessSource_DoesNotInjectAHeaderRow()
    {
        var path = Path.Combine(_dataDir, "headerless.csv");
        File.WriteAllText(path, "1,Widget A,Electronics,150,29.99,true\n2,Widget B,Electronics,0,49.99,false\n");

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv =>
        {
            csv.HasHeader = false;
            // No header row to name the columns, so the entity's property order is the contract.
            csv.Delimiter = ',';
        });

        using (var db = new DuckDb(options))
        {
            db.Save<InventoryItem>(WriteBackMode.Overwrite);
        }

        string[] lines = File.ReadAllLines(path);

        // Before the fix this was "column0,column1,..." - a header row written into a file declared
        // to have none, which the next read then ate as data.
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("1,", lines[0]);
        Assert.StartsWith("2,", lines[1]);
    }

    [Fact]
    public void Save_Overwrite_OnHeaderlessSource_StillReadsBackTheSameRowCount()
    {
        var path = Path.Combine(_dataDir, "headerless_roundtrip.csv");
        File.WriteAllText(path, "1,Widget A,Electronics,150,29.99,true\n2,Widget B,Electronics,0,49.99,false\n");

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv => { csv.HasHeader = false; csv.Delimiter = ','; });

        using (var db = new DuckDb(options))
        {
            db.Save<InventoryItem>(WriteBackMode.Overwrite);
        }

        // A fresh handle, because the point is that the file on disk still matches its own config.
        var reopened = new FlatFileOptions();
        reopened.AddCsv<InventoryItem>(path, csv => { csv.HasHeader = false; csv.Delimiter = ','; });

        using var db2 = new DuckDb(reopened);
        Assert.Equal(2, db2.Connection.From<InventoryItem>().Count());
    }

    // ------------------------------------------------------------------
    // Save/Export to an explicit path must carry the source's COPY options
    // ------------------------------------------------------------------

    [Fact]
    public void Save_ToExplicitPath_PreservesTheSourceDelimiter()
    {
        var path = WriteSemicolonSource("semi_save.csv");
        var outputPath = Path.Combine(_dataDir, "semi_save_out.csv");

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv => csv.Delimiter = ';');

        using var db = new DuckDb(options);
        db.Save<InventoryItem>(outputPath);

        string text = File.ReadAllText(outputPath);
        // Before the fix the output came back comma-delimited: a file the source that produced it
        // could not read.
        Assert.Contains(";", text);
        Assert.DoesNotContain("Widget A,", text);
    }

    [Fact]
    public async Task SaveAsync_ToExplicitPath_PreservesTheSourceDelimiter()
    {
        var path = WriteSemicolonSource("semi_save_async.csv");
        var outputPath = Path.Combine(_dataDir, "semi_save_async_out.csv");

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv => csv.Delimiter = ';');

        using var db = new DuckDb(options);
        await db.SaveAsync<InventoryItem>(outputPath);

        Assert.Contains(";", await File.ReadAllTextAsync(outputPath));
    }

    [Fact]
    public void Export_ToSameFormat_PreservesTheSourceDelimiter()
    {
        var path = WriteSemicolonSource("semi_export.csv");
        var outputPath = Path.Combine(_dataDir, "semi_export_out.csv");

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv => csv.Delimiter = ';');

        using var db = new DuckDb(options);
        db.Export<InventoryItem>(outputPath);

        Assert.Contains(";", File.ReadAllText(outputPath));
    }

    [Fact]
    public void Save_ToExplicitPath_PreservesTheSourceNullString()
    {
        var path = Path.Combine(_dataDir, "nullstr.csv");
        File.WriteAllText(path, "ItemId,ItemName,Category,StockQuantity,UnitPrice,InStock\n1,NA,Electronics,150,29.99,true\n");
        var outputPath = Path.Combine(_dataDir, "nullstr_out.csv");

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv => csv.NullString = "NA");

        using var db = new DuckDb(options);
        db.Save<InventoryItem>(outputPath);

        // "NA" was read as NULL, so it has to be written back as "NA" rather than as an empty field.
        Assert.Contains("NA", File.ReadAllText(outputPath));
    }

    // ------------------------------------------------------------------
    // Cross-format export must NOT carry them - CSV options are not valid for PARQUET
    // ------------------------------------------------------------------

    [Fact]
    public void Export_CrossFormat_StillWorks_AndDoesNotLeakCsvOptions()
    {
        var path = WriteSemicolonSource("cross.csv");
        var outputPath = Path.Combine(_dataDir, "cross_out.parquet");

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv => { csv.Delimiter = ';'; csv.QuoteChar = '"'; });

        using var db = new DuckDb(options);

        // DELIMITER/QUOTE are not COPY arguments for PARQUET; passing the source through would turn
        // the documented "CSV source -> Parquet output" export into a DuckDB binder error.
        db.Export<InventoryItem>(outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.True(new FileInfo(outputPath).Length > 0);
    }

    [Fact]
    public async Task ExportAsync_CrossFormat_StillWorks()
    {
        var path = WriteSemicolonSource("cross_async.csv");
        var outputPath = Path.Combine(_dataDir, "cross_async_out.parquet");

        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv => csv.Delimiter = ';');

        using var db = new DuckDb(options);
        await db.ExportAsync<InventoryItem>(outputPath);

        Assert.True(File.Exists(outputPath));
    }

    private string WriteSemicolonSource(string fileName)
    {
        var path = Path.Combine(_dataDir, fileName);

        using var connection = new DuckDBConnection("DataSource=:memory:");
        connection.Open();
        using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            COPY (
                SELECT * FROM (VALUES
                    (1, 'Widget A', 'Electronics', 150, 29.99, true),
                    (2, 'Widget B', 'Hardware', 0, 49.99, false)
                ) AS t(""ItemId"", ""ItemName"", ""Category"", ""StockQuantity"", ""UnitPrice"", ""InStock"")
            ) TO '{path.Replace("\\", "/").Replace("'", "''")}' (HEADER, DELIMITER ';')";
        cmd.ExecuteNonQuery();

        return path;
    }
}
