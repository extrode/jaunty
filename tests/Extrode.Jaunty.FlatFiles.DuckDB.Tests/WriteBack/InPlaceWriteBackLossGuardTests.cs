using System.IO.Compression;

using DuckDB.NET.Data;

using Extrode.Jaunty.FlatFiles.DuckDB.Tests.Helpers.Entities;
using Extrode.Jaunty.FlatFiles.WriteBack;

namespace Extrode.Jaunty.FlatFiles.DuckDB.Tests.WriteBack;

/// <summary>
/// AUD-R38-003/004/005: glob sources slipped past both write-back guards.
/// AUD-R38-006: in-place Save of an Excel source dropped the other sheets and the cells outside Range.
/// AUD-R38-027: in-place Save dropped the preamble SkipRows skips.
/// </summary>
public class InPlaceWriteBackLossGuardTests : IDisposable
{
    private const string Rows =
        "SELECT * FROM (VALUES (1, 'Widget A', 'Electronics', 150, 29.99, true), (2, 'Widget B', 'Electronics', 0, 49.99, false)) " +
        "AS t(\"ItemId\", \"ItemName\", \"Category\", \"StockQuantity\", \"UnitPrice\", \"InStock\")";

    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"jaunty_wb_loss_{Guid.NewGuid():N}");

    public InPlaceWriteBackLossGuardTests() => Directory.CreateDirectory(_dataDir);

    public void Dispose()
    {
        try { Directory.Delete(_dataDir, true); } catch { }
    }

    private string GlobSource()
    {
        Copy(Path.Combine(_dataDir, "part1.csv"), "HEADER, DELIMITER ','", loadExcel: false);
        Copy(Path.Combine(_dataDir, "part2.csv"), "HEADER, DELIMITER ','", loadExcel: false);
        return Path.Combine(_dataDir, "part*.csv");
    }

    [Fact]
    public void Export_ToAFileTheGlobSourceReads_Throws()
    {
        string member = Path.Combine(_dataDir, "part1.csv");
        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(GlobSource());
        string before = File.ReadAllText(member);
        using var db = new DuckDb(options);

        var ex = Assert.Throws<ArgumentException>(() => db.Export<InventoryItem>(member));

        Assert.Equal("outputPath", ex.ParamName);
        Assert.Equal(before, File.ReadAllText(member));
        Assert.Equal(4, db.Query<InventoryItem>("SELECT * FROM inventory").Count);
    }

    [Fact]
    public async Task SaveAsync_ToAFileTheGlobSourceReads_Throws()
    {
        string member = Path.Combine(_dataDir, "part2.csv");
        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(GlobSource());
        using var db = new DuckDb(options);

        await Assert.ThrowsAsync<ArgumentException>(() => db.SaveAsync<InventoryItem>(member).AsTask());
    }

    [Fact]
    public void Export_BesideTheGlobSource_ToANonMember_Writes()
    {
        string output = Path.Combine(_dataDir, "out.csv");
        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(GlobSource());
        using var db = new DuckDb(options);

        db.Export<InventoryItem>(output);

        Assert.Equal(5, File.ReadAllLines(output).Length);
    }

    [Fact]
    public void SaveInPlace_OfAGlobSource_Throws()
    {
        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(GlobSource());
        using var db = new DuckDb(options);

        var ex = Assert.Throws<InvalidOperationException>(() => db.Save<InventoryItem>(WriteBackMode.Overwrite));

        Assert.Contains("not supported for glob sources", ex.Message);
        Assert.Contains($"(source 'inventory' reads the pattern '{Path.Combine(_dataDir, "part*.csv")}', which can match several files). ", ex.Message);
        Assert.Equal(["part1.csv", "part2.csv"], Directory.GetFiles(_dataDir).Select(Path.GetFileName).Order().ToArray());
    }

    [Fact]
    public async Task SaveInPlaceAsync_OfAGlobSource_Throws()
    {
        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(GlobSource());
        using var db = new DuckDb(options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite).AsTask());

        Assert.Contains("not supported for glob sources", ex.Message);
    }

    [Fact]
    public void SaveInPlace_OfACsvSourceWithSkipRows_ThrowsAndLeavesThePreamble()
    {
        string path = Path.Combine(_dataDir, "preamble.csv");
        File.WriteAllText(path, "# export v2\nItemId,ItemName,Category,StockQuantity,UnitPrice,InStock\n1,Widget A,Electronics,150,29.99,true\n");
        var options = new FlatFileOptions();
        options.AddCsv<InventoryItem>(path, csv => csv.SkipRows = 1);
        string before = File.ReadAllText(path);
        using var db = new DuckDb(options);

        var ex = Assert.Throws<InvalidOperationException>(() => db.Save<InventoryItem>(WriteBackMode.Overwrite));

        Assert.StartsWith($"In-place WriteBack (WriteBackMode) of source 'inventory' would rewrite '{path}' from only the data the source reads, losing the 1 line(s) SkipRows skips before the header.", ex.Message);
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Fact]
    public async Task SaveInPlaceAsync_OfATsvSourceWithSkipRows_Throws()
    {
        string path = Path.Combine(_dataDir, "preamble.tsv");
        File.WriteAllText(path, "# a\n# b\nItemId\tItemName\tCategory\tStockQuantity\tUnitPrice\tInStock\n1\tWidget A\tElectronics\t150\t29.99\ttrue\n");
        var options = new FlatFileOptions();
        options.AddTsv<InventoryItem>(path, tsv => tsv.SkipRows = 2);
        using var db = new DuckDb(options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite).AsTask());

        Assert.Contains("losing the 2 line(s) SkipRows skips", ex.Message);
    }

    [Fact]
    public void SaveInPlace_OfAnExcelSourceWithARange_Throws()
    {
        string path = Path.Combine(_dataDir, "ranged.xlsx");
        Copy(path, "FORMAT XLSX, SHEET 'Data', HEADER true", loadExcel: true);
        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(path, xl => { xl.SheetName = "Data"; xl.Range = "A1:F2"; });
        using var db = new DuckDb(options);

        var ex = Assert.Throws<InvalidOperationException>(() => db.Save<InventoryItem>(WriteBackMode.Overwrite));

        Assert.Contains("losing every cell outside Range 'A1:F2'", ex.Message);
    }

    [Fact]
    public void SaveInPlace_OfAnExcelSourceInATwoSheetWorkbook_ThrowsAndKeepsTheWorkbook()
    {
        string path = Path.Combine(_dataDir, "two_sheets.xlsx");
        Copy(path, "FORMAT XLSX, SHEET 'Data', HEADER true", loadExcel: true);
        AddSecondSheet(path);
        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(path, xl => xl.SheetName = "Data");
        using var db = new DuckDb(options);

        var ex = Assert.Throws<InvalidOperationException>(() => db.Save<InventoryItem>(WriteBackMode.Overwrite));

        Assert.Contains("losing the workbook's other 1 sheet(s)", ex.Message);
        Assert.Equal(2, DuckDb.CountWorkbookSheets(path));
    }

    [Fact]
    public async Task SaveInPlaceAsync_OfAnExcelSourceInATwoSheetWorkbook_Throws()
    {
        string path = Path.Combine(_dataDir, "two_sheets_async.xlsx");
        Copy(path, "FORMAT XLSX, SHEET 'Data', HEADER true", loadExcel: true);
        AddSecondSheet(path);
        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(path, xl => xl.SheetName = "Data");
        using var db = new DuckDb(options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveAsync<InventoryItem>(WriteBackMode.Overwrite).AsTask());
    }

    [Fact]
    public void SaveInPlace_OfASingleSheetExcelSource_StillWorks()
    {
        string path = Path.Combine(_dataDir, "single.xlsx");
        Copy(path, "FORMAT XLSX, SHEET 'Data', HEADER true", loadExcel: true);
        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(path, xl => xl.SheetName = "Data");
        using (var db = new DuckDb(options))
        {
            db.Delete<InventoryItem>(i => i.ItemId == 2);
            db.Save<InventoryItem>(WriteBackMode.Overwrite);
        }

        var reopened = new FlatFileOptions();
        reopened.AddExcel<InventoryItem>(path, xl => xl.SheetName = "Data");
        using var check = new DuckDb(reopened);
        Assert.Single(check.Query<InventoryItem>("SELECT * FROM inventory"));
    }

    [Fact]
    public void SaveInPlace_OfAnExcelSourceWhoseFileIsNotAWorkbook_Throws()
    {
        string path = Path.Combine(_dataDir, "replaced.xlsx");
        Copy(path, "FORMAT XLSX, SHEET 'Data', HEADER true", loadExcel: true);
        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(path, xl => xl.SheetName = "Data");
        using var db = new DuckDb(options);
        File.WriteAllText(path, "plain text");

        var ex = Assert.Throws<InvalidOperationException>(() => db.Save<InventoryItem>(WriteBackMode.Overwrite));

        Assert.Contains("losing any sheet other than the one it reads (the workbook's sheets could not be listed: ", ex.Message);
        Assert.Equal("plain text", File.ReadAllText(path));
    }

    [Fact]
    public void SaveInPlace_OfAnExcelSourceWithMalformedWorkbookXml_Throws()
    {
        string path = Path.Combine(_dataDir, "malformed.xlsx");
        Copy(path, "FORMAT XLSX, SHEET 'Data', HEADER true", loadExcel: true);
        var options = new FlatFileOptions();
        options.AddExcel<InventoryItem>(path, xl => xl.SheetName = "Data");
        using var db = new DuckDb(options);
        File.Delete(path);
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("xl/workbook.xml").Open()))
            writer.Write("<workbook><sheets><sheet");

        var ex = Assert.Throws<InvalidOperationException>(() => db.Save<InventoryItem>(WriteBackMode.Overwrite));

        Assert.Contains("the workbook's sheets could not be listed", ex.Message);
    }

    [Fact]
    public void CountWorkbookSheets_OfAFileThatIsNotAWorkbook_Throws()
    {
        string path = Path.Combine(_dataDir, "not.xlsx");
        File.WriteAllText(path, "plain text");

        Assert.Throws<InvalidDataException>(() => DuckDb.CountWorkbookSheets(path));
    }

    [Fact]
    public void CountWorkbookSheets_OfAZipWithoutAWorkbookPart_Throws()
    {
        string path = Path.Combine(_dataDir, "empty.xlsx");
        using (ZipFile.Open(path, ZipArchiveMode.Create)) { }

        var ex = Assert.Throws<InvalidDataException>(() => DuckDb.CountWorkbookSheets(path));

        Assert.Equal("xl/workbook.xml is missing.", ex.Message);
    }

    private static void AddSecondSheet(string path)
    {
        var entries = new List<(string Name, byte[] Content)>();
        using (ZipArchive source = ZipFile.OpenRead(path))
        {
            foreach (ZipArchiveEntry entry in source.Entries)
            {
                using var buffer = new MemoryStream();
                using (Stream stream = entry.Open())
                    stream.CopyTo(buffer);
                entries.Add((entry.FullName, buffer.ToArray()));
            }
        }

        int index = entries.FindIndex(e => e.Name == "xl/workbook.xml");
        string xml = System.Text.Encoding.UTF8.GetString(entries[index].Content);
        int end = xml.IndexOf("</sheets>", StringComparison.Ordinal);
        Assert.True(end > 0);
        entries[index] = (entries[index].Name, System.Text.Encoding.UTF8.GetBytes(xml.Insert(end, "<sheet name=\"Summary\" sheetId=\"99\" r:id=\"rIdSummary\"/>")));

        File.Delete(path);
        using ZipArchive target = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string name, byte[] content) in entries)
        {
            using Stream stream = target.CreateEntry(name).Open();
            stream.Write(content);
        }
    }

    private static void Copy(string path, string copyOptions, bool loadExcel)
    {
        using var conn = new DuckDBConnection("DataSource=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();
        if (loadExcel)
        {
            cmd.CommandText = "INSTALL excel; LOAD excel;";
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = $"COPY ({Rows}) TO '{path.Replace("\\", "/").Replace("'", "''")}' ({copyOptions})";
        cmd.ExecuteNonQuery();
    }
}
